using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using NpuTools.Obsidian.Models;

namespace NpuTools.Obsidian.Services;

internal sealed partial class ObsidianVaultStore
{
    private static readonly HashSet<string> IgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".obsidian", ".trash",
    };

    private readonly ObsidianSettingsStore _settings;
    private readonly ObsidianMetadataStore _metadata;

    public ObsidianVaultStore(ObsidianSettingsStore settings, ObsidianMetadataStore metadata)
    {
        _settings = settings;
        _metadata = metadata;
    }

    public bool IsVaultConfigured()
    {
        string vaultPath = _settings.Current.VaultPath;
        return !string.IsNullOrWhiteSpace(vaultPath) && Directory.Exists(vaultPath);
    }

    public IReadOnlyList<ObsidianNote> GetAll()
    {
        var settings = _settings.Current;
        if (!IsVaultConfigured())
            return [];

        var notes = new List<ObsidianNote>();
        foreach (string path in EnumerateMarkdownFiles(settings.VaultPath))
        {
            var note = TryLoad(path, settings.VaultPath);
            if (note is not null)
            {
                _metadata.Apply(note);
                notes.Add(note);
            }
        }

        _metadata.Prune(notes);
        notes.Sort((a, b) => b.LastModifiedUtc.CompareTo(a.LastModifiedUtc));
        return notes;
    }

    public IReadOnlyList<ObsidianNote> GetRecent(int maxCount)
    {
        return GetAll()
            .OrderByDescending(n => n.IsPinned)
            .ThenBy(n => n.PinOrder ?? int.MaxValue)
            .ThenByDescending(n => n.LastOpenedUtc ?? n.LastModifiedUtc)
            .Take(maxCount)
            .ToList();
    }

    public ObsidianNote? GetByPath(string absolutePath)
    {
        var settings = _settings.Current;
        string full = Path.GetFullPath(absolutePath);
        if (!File.Exists(full))
            return null;

        var note = TryLoad(full, settings.VaultPath);
        if (note is not null)
            _metadata.Apply(note);
        return note;
    }

    public void RecordOpened(ObsidianNote note) => _metadata.RecordOpened(note);

    public void SetPinned(ObsidianNote note, bool pinned) => _metadata.SetPinned(note, pinned);

    public ObsidianNote RenameNote(ObsidianNote note, string newTitle)
    {
        string currentVaultPath = _settings.Current.VaultPath;
        string vaultPath = Path.GetFullPath(currentVaultPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string sourcePath = Path.GetFullPath(note.AbsolutePath);

        if (!sourcePath.StartsWith(vaultPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Note is outside the configured vault: {sourcePath}");

        string content = File.ReadAllText(sourcePath, Encoding.UTF8);
        string updated = UpdateFirstH1(content, newTitle);

        string slug = ObsidianMarkdownParser.Slugify(newTitle);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "untitled";

        string dir = Path.GetDirectoryName(sourcePath)!;
        string newPath = ResolveCollision(Path.Combine(dir, slug + ".md"));
        bool pathChanged = !string.Equals(sourcePath, newPath, StringComparison.OrdinalIgnoreCase);

        if (pathChanged)
        {
            File.Move(sourcePath, newPath); // atomic rename; no duplicate risk
            WriteAtomic(newPath, updated, overwrite: true);
        }
        else
        {
            WriteAtomic(sourcePath, updated, overwrite: true);
        }

        var newNote = TryLoad(newPath, currentVaultPath)
            ?? throw new IOException($"Renamed note could not be read back from '{newPath}'.");
        _metadata.Remap(note, newNote);
        _metadata.Apply(newNote);
        return newNote;
    }

    public ObsidianNote MoveNote(ObsidianNote note, string targetRelativeDir)
    {
        string currentVaultPath = _settings.Current.VaultPath;
        string vaultPath = Path.GetFullPath(currentVaultPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string sourcePath = Path.GetFullPath(note.AbsolutePath);
        string vaultWithSep = vaultPath + Path.DirectorySeparatorChar;

        if (!sourcePath.StartsWith(vaultWithSep, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Note is outside the configured vault: {sourcePath}");

        string targetDir = string.IsNullOrWhiteSpace(targetRelativeDir)
            ? vaultPath
            : Path.GetFullPath(Path.Combine(vaultPath, targetRelativeDir));

        // Guard: target must equal the vault root or be a proper subdirectory.
        if (!string.Equals(targetDir, vaultPath, StringComparison.OrdinalIgnoreCase)
            && !targetDir.StartsWith(vaultWithSep, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Target directory is outside the configured vault: {targetDir}");

        Directory.CreateDirectory(targetDir);

        string fileName = Path.GetFileName(sourcePath);
        string newPath = ResolveCollision(Path.Combine(targetDir, fileName));

        if (string.Equals(sourcePath, newPath, StringComparison.OrdinalIgnoreCase))
            return note;

        File.Move(sourcePath, newPath);

        var newNote = TryLoad(newPath, currentVaultPath)
            ?? throw new IOException($"Moved note could not be read back from '{newPath}'.");
        _metadata.Remap(note, newNote);
        _metadata.Apply(newNote);
        return newNote;
    }

    public IReadOnlyList<string> GetVaultSubfolders()
    {
        string vaultPath = _settings.Current.VaultPath;
        if (!Directory.Exists(vaultPath))
            return [];

        var folders = new List<string>();
        foreach (string dir in Directory.EnumerateDirectories(vaultPath, "*", System.IO.SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(dir);
            if (ObsidianVaultStore.IgnoredDirs.Contains(name))
                continue;

            string relative = Path.GetRelativePath(vaultPath, dir);
            folders.Add(relative);
        }

        folders.Sort(StringComparer.OrdinalIgnoreCase);
        return folders;
    }

    private static string UpdateFirstH1(string markdown, string newTitle)
    {
        string normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        int bodyStart = 0;

        // Skip YAML frontmatter.
        if (lines.Length > 0 && lines[0] == "---")
        {
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i] == "---")
                {
                    bodyStart = i + 1;
                    break;
                }
            }
        }

        for (int i = bodyStart; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("# ", StringComparison.Ordinal))
            {
                lines[i] = $"# {newTitle}";
                return string.Join("\n", lines);
            }
        }

        // No H1 found — prepend one after frontmatter.
        var result = new System.Collections.Generic.List<string>(lines);
        result.Insert(bodyStart, string.Empty);
        result.Insert(bodyStart, $"# {newTitle}");
        return string.Join("\n", result);
    }

    public void DeleteToRecycleBin(ObsidianNote note)
    {
        string vaultPath = Path.GetFullPath(_settings.Current.VaultPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string targetPath = Path.GetFullPath(note.AbsolutePath);

        if (!targetPath.StartsWith(vaultPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Delete target is outside the configured vault: {targetPath}");

        if (!string.Equals(Path.GetExtension(targetPath), ".md", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Delete target is not a Markdown file: {targetPath}");

        if (!File.Exists(targetPath))
            return;

        try
        {
            FileSystem.DeleteFile(targetPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianVaultStore.DeleteToRecycleBin failed: {ex.GetType().Name}: {ex.Message}");
            return;
        }
        _metadata.Remove(note);
    }

    public ObsidianNote Create(string title, string body, string? subfolder = null)
    {
        var settings = _settings.Current;
        if (!IsVaultConfigured())
            throw new InvalidOperationException("Vault path is not configured.");

        string targetDir = string.IsNullOrWhiteSpace(subfolder)
            ? (string.IsNullOrWhiteSpace(settings.DefaultNewNoteFolder) ? settings.VaultPath : Path.Combine(settings.VaultPath, settings.DefaultNewNoteFolder))
            : Path.Combine(settings.VaultPath, subfolder);
        Directory.CreateDirectory(targetDir);

        string slug = ObsidianMarkdownParser.Slugify(title);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "untitled";

        string fileName = $"{slug}.md";
        string path = ResolveCollision(Path.Combine(targetDir, fileName));

        string markdown = BuildMarkdown(title, body);
        WriteAtomic(path, markdown);

        var note = TryLoad(path, settings.VaultPath) ?? throw new IOException("Created note could not be read back.");
        _metadata.RecordOpened(note);
        _metadata.Apply(note);
        return note;
    }

    public static void AppendToNote(ObsidianNote note, string text)
    {
        if (!File.Exists(note.AbsolutePath))
            throw new FileNotFoundException("Note file not found.", note.AbsolutePath);

        string current = File.ReadAllText(note.AbsolutePath, Encoding.UTF8);
        string separator = current.EndsWith('\n') ? "" : Environment.NewLine;
        string appended = current + separator + text.Trim() + Environment.NewLine;
        WriteAtomic(note.AbsolutePath, appended);
    }

    internal static ObsidianNote ParseMarkdown(string absolutePath, string vaultPath, string markdown, DateTime lastModifiedUtc)
        => ObsidianMarkdownParser.ParseMarkdown(absolutePath, vaultPath, markdown, lastModifiedUtc);

    internal static List<string> ExtractWikiLinks(string body)
        => ObsidianMarkdownParser.ExtractWikiLinks(body);

    internal static IEnumerable<string> EnumerateMarkdownFiles(string vaultPath)
    {
        if (!Directory.Exists(vaultPath))
            yield break;

        var stack = new Stack<string>();
        stack.Push(vaultPath);

        while (stack.Count > 0)
        {
            string dir = stack.Pop();
            string dirName = Path.GetFileName(dir);

            if (!string.Equals(dir, vaultPath, StringComparison.OrdinalIgnoreCase) && IgnoredDirs.Contains(dirName))
                continue;

            foreach (string file in Directory.EnumerateFiles(dir, "*.md"))
                yield return file;

            foreach (string subDir in Directory.EnumerateDirectories(dir))
                stack.Push(subDir);
        }
    }

    private static ObsidianNote? TryLoad(string path, string vaultPath)
    {
        try
        {
            var info = new FileInfo(path);
            string markdown = File.ReadAllText(path, Encoding.UTF8);
            return ObsidianMarkdownParser.ParseMarkdown(path, vaultPath, markdown, info.LastWriteTimeUtc);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianVaultStore.TryLoad failed for '{path}': {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string BuildMarkdown(string title, string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine(FormattableString.Invariant($"# {title}"));
        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.AppendLine(body.Trim());
        }

        return sb.ToString();
    }

    private static string ResolveCollision(string path)
    {
        if (!File.Exists(path))
            return path;

        string dir = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        for (int i = 2; i < 1000; i++)
        {
            string candidate = Path.Combine(dir, $"{name}-{i}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name}-{Guid.NewGuid():N}{ext}");
    }

    private static void WriteAtomic(string path, string content, bool overwrite = false)
    {
        string tmp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        File.Move(tmp, path, overwrite);
    }
}
