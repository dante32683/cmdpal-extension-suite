using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        string slug = Slugify(title);
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
    {
        string body = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var frontmatterLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (body.StartsWith("---\n", StringComparison.Ordinal))
        {
            int end = body.IndexOf("\n---\n", 4, StringComparison.Ordinal);
            if (end >= 0)
            {
                string header = body[4..end];
                body = body[(end + 5)..];
                ParseFrontmatter(header, frontmatter, frontmatterLists);
            }
        }

        string relative = Path.GetRelativePath(vaultPath, absolutePath);
        string title = ResolveTitleFromFrontmatter(frontmatter, body, absolutePath);
        List<string> tags = ResolveTags(frontmatter, frontmatterLists, body);
        List<string> aliases = frontmatterLists.TryGetValue("aliases", out var aliasList)
            ? aliasList
            : (frontmatter.TryGetValue("alias", out string? alias) ? ParseCsv(alias) : []);
        List<string> headings = ExtractHeadings(body);

        return new ObsidianNote
        {
            AbsolutePath = absolutePath,
            RelativePath = relative,
            VaultPath = vaultPath,
            Title = title,
            Tags = tags,
            Aliases = aliases,
            Headings = headings,
            Body = body.Trim(),
            LastModifiedUtc = new DateTimeOffset(DateTime.SpecifyKind(lastModifiedUtc, DateTimeKind.Utc)),
        };
    }

    internal static string Slugify(string value, int maxLength = 80)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        bool lastWasDash = false;
        foreach (char c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            char lower = char.ToLowerInvariant(c);
            if (char.IsLetterOrDigit(lower))
            {
                sb.Append(lower);
                lastWasDash = false;
            }
            else if (!lastWasDash && sb.Length > 0)
            {
                sb.Append('-');
                lastWasDash = true;
            }

            if (sb.Length >= maxLength)
                break;
        }

        return sb.ToString().Trim('-');
    }

    private static void ParseFrontmatter(string header, Dictionary<string, string> scalars, Dictionary<string, List<string>> lists)
    {
        string? currentListKey = null;
        var currentListValues = new List<string>();

        foreach (string rawLine in header.Split('\n'))
        {
            string line = rawLine.TrimEnd();

            if (line.StartsWith("  - ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            {
                string item = line.TrimStart().TrimStart('-').Trim();
                if (currentListKey is not null && item.Length > 0)
                    currentListValues.Add(item);
                continue;
            }

            if (currentListKey is not null)
            {
                lists[currentListKey] = new List<string>(currentListValues);
                currentListValues.Clear();
                currentListKey = null;
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
                continue;

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();

            if (value.Length == 0)
            {
                currentListKey = key;
            }
            else if (value.StartsWith('[') && value.EndsWith(']'))
            {
                string inner = value[1..^1];
                lists[key] = ParseCsv(inner);
            }
            else
            {
                scalars[key] = value;
            }
        }

        if (currentListKey is not null && currentListValues.Count > 0)
            lists[currentListKey] = new List<string>(currentListValues);
    }

    private static string ResolveTitleFromFrontmatter(Dictionary<string, string> frontmatter, string body, string absolutePath)
    {
        if (frontmatter.TryGetValue("title", out string? fmTitle) && !string.IsNullOrWhiteSpace(fmTitle))
            return TrimTitle(fmTitle.Trim('"').Trim());

        foreach (string rawLine in body.Split('\n', StringSplitOptions.TrimEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("# ", StringComparison.Ordinal))
                return TrimTitle(line[2..].Trim());
            break;
        }

        return TrimTitle(Path.GetFileNameWithoutExtension(absolutePath));
    }

    private static List<string> ResolveTags(
        Dictionary<string, string> frontmatter,
        Dictionary<string, List<string>> frontmatterLists,
        string body)
    {
        var tags = new List<string>();

        if (frontmatterLists.TryGetValue("tags", out var tagList))
            tags.AddRange(tagList);
        else if (frontmatter.TryGetValue("tags", out string? tagsCsv))
            tags.AddRange(ParseCsv(tagsCsv));

        foreach (Match match in InlineTagPattern().Matches(body))
        {
            string tag = match.Groups[1].Value;
            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                tags.Add(tag);
        }

        return tags;
    }

    private static List<string> ExtractHeadings(string body)
    {
        var headings = new List<string>();
        foreach (string rawLine in body.Split('\n', StringSplitOptions.TrimEntries))
        {
            string line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                string heading = line.TrimStart('#').Trim();
                if (heading.Length > 0)
                    headings.Add(heading);
            }
        }

        return headings;
    }

    private static List<string> ParseCsv(string value)
    {
        return value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .Select(t => t.Trim('"').Trim('\'').Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> EnumerateMarkdownFiles(string vaultPath)
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
            return ParseMarkdown(path, vaultPath, markdown, info.LastWriteTimeUtc);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianVaultStore TryLoad failed for {path}: {ex.GetType().Name}: {ex.Message}");
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

    private static string TrimTitle(string title)
    {
        string trimmed = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        return trimmed.Length > 120 ? trimmed[..120].Trim() : trimmed;
    }

    [GeneratedRegex(@"(?<!\w)#([A-Za-z][A-Za-z0-9_/-]*)")]
    private static partial Regex InlineTagPattern();
}
