using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using NpuTools.Notes.Models;

namespace NpuTools.Notes.Services;

internal sealed partial class NotesStore
{
    private static readonly string[] Categories =
    [
        "work",
        "school",
        "personal",
        "tasks",
        "ideas",
        "health",
        "finance",
        "people",
        "projects",
        "misc",
    ];

    private readonly NotesSettingsStore _settings;
    private readonly NotesIndexStore _index;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, (NoteEntry Entry, DateTime LastWriteTimeUtc)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public NotesStore(NotesSettingsStore settings, NotesIndexStore index)
    {
        _settings = settings;
        _index = index;
    }

    public static IReadOnlyList<string> KnownCategories => Categories;

    public IReadOnlyList<NoteEntry> GetAll()
    {
        var settings = _settings.Current;
        EnsureRoot(settings.NotesRoot);

        var entries = new List<NoteEntry>();
        lock (_cacheLock)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string path in Directory.EnumerateFiles(settings.NotesRoot, "*.md", System.IO.SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(path).StartsWith('.'))
                        continue;

                    files.Add(path);

                    try
                    {
                        var writeTime = File.GetLastWriteTimeUtc(path);
                        if (_cache.TryGetValue(path, out var cached) && cached.LastWriteTimeUtc == writeTime)
                        {
                            entries.Add(cached.Entry);
                        }
                        else
                        {
                            var entry = TryLoad(path, settings.NotesRoot);
                            if (entry is not null)
                            {
                                _cache[path] = (entry, writeTime);
                                entries.Add(entry);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"NotesStore load/cache failed for '{path}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"NotesStore directory enumeration failed: {ex.Message}");
            }

            // Remove deleted files from the cache
            var toRemove = _cache.Keys.Where(k => !files.Contains(k)).ToList();
            foreach (var key in toRemove)
            {
                _cache.Remove(key);
            }
        }

        foreach (var entry in entries)
        {
            _index.Apply(entry);
        }
        _index.Prune(entries);
        entries.Sort(CompareByPinnedThenUpdated);
        return entries;
    }

    public IReadOnlyList<NoteEntry> GetRecent(int maxCount)
    {
        return GetAll()
            .OrderByDescending(e => e.IsPinned)
            .ThenBy(e => e.PinOrder ?? int.MaxValue)
            .ThenByDescending(e => e.LastOpenedUtc ?? e.UpdatedUtc)
            .Take(maxCount)
            .ToList();
    }

    public IReadOnlyList<NoteEntry> GetByCategory(string category)
    {
        string normalized = NormalizeCategory(category);
        return GetAll().Where(e => string.Equals(e.Category, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public NoteEntry? GetByPath(string path)
    {
        var settings = _settings.Current;
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            return null;

        var entry = TryLoad(fullPath, settings.NotesRoot);
        if (entry is not null)
            _index.Apply(entry);
        return entry;
    }

    public NoteEntry Create(string rawText, string? category = null)
    {
        var settings = _settings.Current;
        string normalizedCategory = NormalizeCategory(category ?? settings.DefaultCategory);
        string root = settings.NotesRoot;
        EnsureRoot(root);

        var parsed = ParseRawNote(rawText);
        string id = Guid.NewGuid().ToString();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string fileName = BuildFileName(parsed.Title, now);
        string categoryDir = Path.Combine(root, normalizedCategory);
        Directory.CreateDirectory(categoryDir);
        string path = ResolveCollision(Path.Combine(categoryDir, fileName));

        string markdown = BuildMarkdown(id, parsed.Title, normalizedCategory, now, now, parsed.Body);
        WriteAtomic(path, markdown);

        var entry = TryLoad(path, root) ?? throw new IOException("Created note could not be read back.");
        _index.RecordOpened(entry);
        _index.Apply(entry);
        return entry;
    }

    public void RecordOpened(NoteEntry entry)
    {
        _index.RecordOpened(entry);
    }

    public void SetPinned(NoteEntry entry, bool pinned)
    {
        _index.SetPinned(entry, pinned);
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public void UpdateNote(NoteEntry entry, string newTitle, string newBody)
    {
        if (!File.Exists(entry.FilePath))
            return;

        // Staleness guard: skip if the note was edited after we read it.
        if (TryReadUpdatedUtc(entry.FilePath, out var diskUtc) && diskUtc != entry.UpdatedUtc)
        {
            Debug.WriteLine($"NotesStore.UpdateNote skipped stale write: {entry.FilePath}");
            return;
        }

        string markdown = BuildMarkdown(entry.Id, newTitle, entry.Category, entry.CreatedUtc, DateTimeOffset.UtcNow, newBody);
        WriteAtomic(entry.FilePath, markdown, overwrite: true);
    }

    internal static bool TryReadUpdatedUtc(string filePath, out DateTimeOffset updatedUtc)
    {
        updatedUtc = default;
        try
        {
            bool inFrontmatter = false;
            foreach (string line in File.ReadLines(filePath))
            {
                if (!inFrontmatter)
                {
                    if (line == "---")
                        inFrontmatter = true;
                    else
                        return false;
                    continue;
                }

                if (line == "---")
                    return false;

                if (line.StartsWith("updatedUtc:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line["updatedUtc:".Length..].Trim();
                    return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out updatedUtc);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesStore.TryReadUpdatedUtc failed for {filePath}: {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    public NoteEntry RenameNote(NoteEntry entry, string newTitle)
    {
        string root = _settings.Current.NotesRoot;
        string srcPath = Path.GetFullPath(entry.FilePath);
        string trimmedTitle = TrimTitle(newTitle);
        string markdown = BuildMarkdown(entry.Id, trimmedTitle, entry.Category, entry.CreatedUtc, DateTimeOffset.UtcNow, entry.Body);

        string dir = Path.GetDirectoryName(srcPath)!;
        string slug = Slugify(trimmedTitle);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "untitled-note";

        string datePart = ExtractDatePrefix(Path.GetFileNameWithoutExtension(srcPath));
        string newFileName = $"{datePart}{slug}.md";
        string newPath = ResolveCollision(Path.Combine(dir, newFileName));
        bool pathChanged = !string.Equals(srcPath, newPath, StringComparison.OrdinalIgnoreCase);

        if (pathChanged)
        {
            File.Move(srcPath, newPath); // atomic rename; no duplicate risk
            WriteAtomic(newPath, markdown, overwrite: true);
        }
        else
        {
            WriteAtomic(srcPath, markdown, overwrite: true);
        }

        var newEntry = TryLoad(newPath, root) ?? throw new IOException($"Renamed note could not be read back from '{newPath}'.");
        _index.Remap(entry, newEntry);
        _index.Apply(newEntry);
        return newEntry;
    }

    public NoteEntry MoveNote(NoteEntry entry, string targetCategory)
    {
        string root = _settings.Current.NotesRoot;
        string srcPath = Path.GetFullPath(entry.FilePath);
        string normalized = NormalizeCategory(targetCategory);

        string targetDir = Path.Combine(root, normalized);
        Directory.CreateDirectory(targetDir);

        string fileName = Path.GetFileName(srcPath);
        string newPath = ResolveCollision(Path.Combine(targetDir, fileName));

        if (string.Equals(srcPath, newPath, StringComparison.OrdinalIgnoreCase))
            return entry;

        string markdown = BuildMarkdown(entry.Id, entry.Title, normalized, entry.CreatedUtc, entry.UpdatedUtc, entry.Body);
        File.Move(srcPath, newPath); // atomic rename; no duplicate risk
        WriteAtomic(newPath, markdown, overwrite: true);

        var newEntry = TryLoad(newPath, root) ?? throw new IOException($"Moved note could not be read back from '{newPath}'.");
        _index.Remap(entry, newEntry);
        _index.Apply(newEntry);
        return newEntry;
    }

    public void DeleteToRecycleBin(NoteEntry entry)
    {
        if (!File.Exists(entry.FilePath))
            return;

        try
        {
            FileSystem.DeleteFile(entry.FilePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesStore.DeleteToRecycleBin failed: {ex.GetType().Name}: {ex.Message}");
            return;
        }
        _index.Remove(entry);
    }

    public static string NormalizeCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "misc";

        string normalized = Slugify(value, maxLength: 40);
        return Categories.Contains(normalized, StringComparer.OrdinalIgnoreCase) ? normalized : "misc";
    }

    internal static ParsedNote ParseRawNote(string? rawText)
    {
        string text = rawText?.Replace("\r\n", "\n").Replace('\r', '\n').Trim() ?? string.Empty;
        if (text.Length == 0)
            return new ParsedNote("Untitled Note", string.Empty);

        string[] lines = text.Split('\n');
        string title = "Untitled Note";
        int titleIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            title = line.TrimStart('#').Trim();
            titleIndex = i;
            break;
        }

        var bodyLines = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i == titleIndex)
                continue;
            bodyLines.Add(lines[i]);
        }

        return new ParsedNote(TrimTitle(title), string.Join('\n', bodyLines).Trim());
    }

    internal static NoteEntry ParseMarkdown(string path, string root, string markdown, DateTime createdUtc, DateTime updatedUtc)
    {
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string body = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        if (body.StartsWith("---\n", StringComparison.Ordinal))
        {
            int end = body.IndexOf("\n---\n", 4, StringComparison.Ordinal);
            if (end >= 0)
            {
                string header = body[4..end];
                body = body[(end + 5)..];
                foreach (string line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    int colon = line.IndexOf(':', StringComparison.Ordinal);
                    if (colon <= 0)
                        continue;
                    frontmatter[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }
            }
        }

        string relative = Path.GetRelativePath(root, path);
        string category = frontmatter.TryGetValue("category", out string? cat)
            ? NormalizeCategory(cat)
            : NormalizeCategory(Path.GetDirectoryName(relative)?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault());

        string title = frontmatter.TryGetValue("title", out string? fmTitle) && !string.IsNullOrWhiteSpace(fmTitle)
            ? fmTitle.Trim()
            : FindTitleFromBody(body);

        return new NoteEntry
        {
            Id = frontmatter.TryGetValue("id", out string? id) && !string.IsNullOrWhiteSpace(id) ? id.Trim() : Path.GetFileNameWithoutExtension(path),
            Title = TrimTitle(title),
            Category = category,
            FilePath = path,
            RelativePath = relative,
            CreatedUtc = ParseDate(frontmatter, "createdUtc", createdUtc),
            UpdatedUtc = ParseDate(frontmatter, "updatedUtc", updatedUtc),
            Tags = ParseTags(frontmatter.TryGetValue("tags", out string? tags) ? tags : null),
            Body = body.Trim(),
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

    private static NoteEntry? TryLoad(string path, string root)
    {
        try
        {
            var info = new FileInfo(path);
            string markdown = File.ReadAllText(path);
            return ParseMarkdown(path, root, markdown, info.CreationTimeUtc, info.LastWriteTimeUtc);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesStore TryLoad failed for {path}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static void EnsureRoot(string root)
    {
        Directory.CreateDirectory(root);
        foreach (string category in Categories)
            Directory.CreateDirectory(Path.Combine(root, category));
    }

    private static string ExtractDatePrefix(string nameWithoutExtension)
    {
        // Format: yyyy-MM-dd_HHmm_slug → date prefix is first 15 chars + underscore
        if (nameWithoutExtension.Length > 16 && nameWithoutExtension[15] == '_')
        {
            string potentialDate = nameWithoutExtension[..15];
            if (DateTime.TryParseExact(potentialDate, "yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return potentialDate + "_";
        }

        return DateTimeOffset.Now.LocalDateTime.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture) + "_";
    }

    private static string BuildFileName(string title, DateTimeOffset now)
    {
        string slug = Slugify(title);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "untitled-note";
        return $"{now.LocalDateTime:yyyy-MM-dd_HHmm}_{slug}.md";
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

    private static string BuildMarkdown(string id, string title, string category, DateTimeOffset createdUtc, DateTimeOffset updatedUtc, string body)
    {
        string escapedTitle = title.Replace("\"", "\\\"", StringComparison.Ordinal);
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine(FormattableString.Invariant($"id: {id}"));
        builder.AppendLine(FormattableString.Invariant($"title: {escapedTitle}"));
        builder.AppendLine(FormattableString.Invariant($"category: {category}"));
        builder.AppendLine(FormattableString.Invariant($"createdUtc: {createdUtc:O}"));
        builder.AppendLine(FormattableString.Invariant($"updatedUtc: {updatedUtc:O}"));
        builder.AppendLine("tags:");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(FormattableString.Invariant($"# {title}"));
        if (!string.IsNullOrWhiteSpace(body))
        {
            builder.AppendLine();
            builder.AppendLine(body.Trim());
        }

        return builder.ToString();
    }

    private static void WriteAtomic(string path, string content, bool overwrite = false)
    {
        string tmp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);
        File.Move(tmp, path, overwrite);
    }

    private static int CompareByPinnedThenUpdated(NoteEntry a, NoteEntry b)
    {
        int pinned = b.IsPinned.CompareTo(a.IsPinned);
        if (pinned != 0)
            return pinned;

        int pinOrder = (a.PinOrder ?? int.MaxValue).CompareTo(b.PinOrder ?? int.MaxValue);
        if (pinOrder != 0)
            return pinOrder;

        return b.UpdatedUtc.CompareTo(a.UpdatedUtc);
    }

    private static string FindTitleFromBody(string body)
    {
        foreach (string rawLine in body.Split('\n', StringSplitOptions.TrimEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith("# ", StringComparison.Ordinal))
                return line[2..].Trim();
            return line.TrimStart('#').Trim();
        }

        return "Untitled Note";
    }

    private static DateTimeOffset ParseDate(Dictionary<string, string> frontmatter, string key, DateTime fallbackUtc)
    {
        return frontmatter.TryGetValue(key, out string? value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : new DateTimeOffset(DateTime.SpecifyKind(fallbackUtc, DateTimeKind.Utc));
    }

    private static List<string> ParseTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string TrimTitle(string title)
    {
        string trimmed = string.IsNullOrWhiteSpace(title) ? "Untitled Note" : title.Trim();
        return trimmed.Length > 120 ? trimmed[..120].Trim() : trimmed;
    }

    internal readonly record struct ParsedNote(string Title, string Body);
}
