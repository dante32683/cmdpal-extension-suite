using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NpuTools.Obsidian.Models;

namespace NpuTools.Obsidian.Services;

internal sealed partial class ObsidianIndexStore
{
    private readonly object _lock = new();
    private List<ObsidianIndexEntry> _entries = [];
    private bool _loaded;

    public bool IsIndexed
    {
        get { lock (_lock) { return _loaded && _entries.Count > 0; } }
    }

    public int EntryCount
    {
        get { lock (_lock) { return _entries.Count; } }
    }

    public void EnsureLoaded()
    {
        lock (_lock)
        {
            if (_loaded)
                return;
            _loaded = true;
            _entries = LoadFromDisk();
        }
    }

    public IReadOnlyList<ObsidianIndexEntry> GetAll()
    {
        EnsureLoaded();
        lock (_lock) { return [.. _entries]; }
    }

    public ObsidianIndexEntry? Get(string absolutePath)
    {
        EnsureLoaded();
        lock (_lock)
        {
            return _entries.FirstOrDefault(e => SamePath(e.AbsolutePath, absolutePath));
        }
    }

    public bool NeedsReindex(string absolutePath, DateTimeOffset lastModified, long fileSize)
    {
        EnsureLoaded();
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => SamePath(e.AbsolutePath, absolutePath));
            if (entry is null)
                return true;
            return entry.LastModifiedUtc != lastModified || entry.FileSizeBytes != fileSize;
        }
    }

    // Rebuilds the full index asynchronously. progress: (filesProcessed, totalFiles, currentFileName).
    public async Task<(int indexed, int skipped, int failed)> RebuildAsync(
        string vaultPath,
        IProgress<(int done, int total, string? current)>? progress = null,
        CancellationToken ct = default)
    {
        var files = ObsidianVaultStore.EnumerateMarkdownFiles(vaultPath).ToList();
        int total = files.Count;
        var newEntries = new List<ObsidianIndexEntry>(total);
        int indexed = 0;
        int skipped = 0;
        int failed = 0;

        for (int i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string path = files[i];
            progress?.Report((i, total, Path.GetFileName(path)));

            var info = new FileInfo(path);

            if (!NeedsReindex(path, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), info.Length))
            {
                var existing = Get(path);
                if (existing is not null)
                {
                    newEntries.Add(existing);
                    skipped++;
                    continue;
                }
            }

            var entry = await Task.Run(() => IndexFile(path, vaultPath), ct);
            if (entry is not null)
            {
                newEntries.Add(entry);
                indexed++;
            }
            else
            {
                failed++;
            }
        }

        BuildBacklinks(newEntries);

        lock (_lock)
        {
            _entries = newEntries;
            _loaded = true;
        }

        SaveToDisk(newEntries);
        progress?.Report((total, total, null));

        return (indexed, skipped, failed);
    }

    // Returns notes built from index data — no file I/O after initial index load.
    public void UpdateSummary(string absolutePath, string summary)
    {
        EnsureLoaded();
        List<ObsidianIndexEntry> snapshot;
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => SamePath(e.AbsolutePath, absolutePath));
            if (entry is null)
                return;
            entry.AiSummary = summary;
            snapshot = [.. _entries];
        }
        SaveToDisk(snapshot);
    }

    public IReadOnlyList<ObsidianNote> GetSearchableNotes(string vaultPath, ObsidianMetadataStore? metadata = null)
    {
        EnsureLoaded();
        List<ObsidianIndexEntry> entries;
        lock (_lock) { entries = [.. _entries]; }

        var notes = new List<ObsidianNote>(entries.Count);
        foreach (var e in entries)
        {
            var note = new ObsidianNote
            {
                AbsolutePath = e.AbsolutePath,
                RelativePath = e.RelativePath,
                VaultPath = vaultPath,
                Title = e.Title,
                Tags = e.Tags,
                Aliases = e.Aliases,
                Headings = e.Headings,
                Body = e.BodyText,
                LastModifiedUtc = e.LastModifiedUtc,
                Backlinks = e.Backlinks,
                AiSummary = e.AiSummary,
            };
            metadata?.Apply(note);
            notes.Add(note);
        }

        return notes;
    }

    private static ObsidianIndexEntry? IndexFile(string path, string vaultPath)
    {
        try
        {
            var info = new FileInfo(path);
            string markdown = File.ReadAllText(path, Encoding.UTF8);
            var note = ObsidianVaultStore.ParseMarkdown(path, vaultPath, markdown, info.LastWriteTimeUtc);
            var wikiLinks = ObsidianVaultStore.ExtractWikiLinks(note.Body);
            var mdLinks = ObsidianMarkdownParser.ExtractMarkdownLinks(note.Body);

            // Merge markdown links into wiki links (dedup, wiki links take priority).
            var allLinks = new List<string>(wikiLinks);
            foreach (string lnk in mdLinks)
            {
                if (!allLinks.Contains(lnk, StringComparer.OrdinalIgnoreCase))
                    allLinks.Add(lnk);
            }

            // Cap body at 32 KB to keep the index file manageable.
            string bodyText = note.Body.Length > 32768 ? note.Body[..32768] : note.Body;

            return new ObsidianIndexEntry
            {
                AbsolutePath = path,
                RelativePath = note.RelativePath,
                Title = note.Title,
                Aliases = note.Aliases,
                Tags = note.Tags,
                Headings = note.Headings,
                WikiLinks = allLinks,
                BodyText = bodyText,
                LastModifiedUtc = note.LastModifiedUtc,
                IndexedUtc = DateTimeOffset.UtcNow,
                FileSizeBytes = info.Length,
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianIndexStore.IndexFile failed for '{path}': {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static void BuildBacklinks(List<ObsidianIndexEntry> entries)
    {
        // Clear existing backlinks before rebuilding.
        foreach (var e in entries)
            e.Backlinks = [];

        // Build lookup: title / filename / relative-path-without-extension -> entry.
        // The path key uses forward slashes to match [[Folder/Note]] and [text](Folder/Note.md) links.
        var byName = new Dictionary<string, ObsidianIndexEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            byName.TryAdd(e.Title, e);
            byName.TryAdd(Path.GetFileNameWithoutExtension(e.AbsolutePath), e);
            string relNoExt = Path.ChangeExtension(e.RelativePath.Replace('\\', '/'), null);
            if (!string.IsNullOrEmpty(relNoExt))
                byName.TryAdd(relNoExt, e);
        }

        foreach (var source in entries)
        {
            foreach (string link in source.WikiLinks)
            {
                string normalized = NormalizeLinkTarget(link);
                if (byName.TryGetValue(normalized, out var target) && !SamePath(target.AbsolutePath, source.AbsolutePath))
                {
                    if (!target.Backlinks.Contains(source.RelativePath, StringComparer.OrdinalIgnoreCase))
                        target.Backlinks.Add(source.RelativePath);
                }
            }
        }
    }

    private static string NormalizeLinkTarget(string link)
    {
        // Strip anchor fragment.
        int hash = link.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
            link = link[..hash].Trim();
        // Strip .md extension (present in Markdown-link targets after merging).
        if (link.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            link = link[..^3];
        // Strip leading ./ path prefix.
        if (link.StartsWith("./", StringComparison.Ordinal))
            link = link[2..];
        return link;
    }

    private static List<ObsidianIndexEntry> LoadFromDisk()
    {
        try
        {
            string path = IndexFilePath();
            if (!File.Exists(path))
                return [];

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, IndexJsonContext.Default.ListObsidianIndexEntry) ?? [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianIndexStore.Load failed: {ex.GetType().Name}: {ex.Message}");
            return [];
        }
    }

    private static void SaveToDisk(List<ObsidianIndexEntry> entries)
    {
        try
        {
            string path = IndexFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(entries, IndexJsonContext.Default.ListObsidianIndexEntry));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianIndexStore.Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string IndexFilePath() => Path.Combine(ObsidianPaths.SupportDir(), "vault-index.json");

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    [JsonSerializable(typeof(List<ObsidianIndexEntry>))]
    [JsonSourceGenerationOptions(WriteIndented = false)]
    private sealed partial class IndexJsonContext : JsonSerializerContext { }
}
