using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

    private static void WriteAtomic(string path, string content)
    {
        string tmp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        File.Move(tmp, path, false);
    }
}
