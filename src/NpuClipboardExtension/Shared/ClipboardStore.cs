using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NpuTools.Clipboard.Data;

public sealed class ClipboardStore
{
    private readonly object _lock = new();
    private readonly List<ClipboardEntry> _entries = [];

    public ClipboardStore()
    {
        Load();
    }

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public IReadOnlyList<ClipboardEntry> Snapshot()
    {
        lock (_lock)
            return _entries.Select(Clone).ToArray();
    }

    public ClipboardEntry? Get(string id)
    {
        lock (_lock)
            return _entries.FirstOrDefault(e => e.Id == id) is { } entry ? Clone(entry) : null;
    }

    public void AddOrPromote(ClipboardEntry entry, ClipboardAppSettings settings)
    {
        lock (_lock)
        {
            var existing = _entries.FirstOrDefault(e => e.ContentHash == entry.ContentHash);
            if (existing is not null)
            {
                existing.LastUsedAt = DateTimeOffset.Now;
                existing.CreatedAt = entry.CreatedAt;
                if (!string.IsNullOrWhiteSpace(entry.Title))
                    existing.Title = entry.Title;
                if (!string.IsNullOrWhiteSpace(entry.Text))
                    existing.Text = entry.Text;
                if (!string.IsNullOrWhiteSpace(entry.OcrText))
                    existing.OcrText = entry.OcrText;
                if (!string.IsNullOrWhiteSpace(entry.ImagePath))
                    existing.ImagePath = entry.ImagePath;
                if (entry.FilePaths.Count > 0)
                    existing.FilePaths = [.. entry.FilePaths];
                existing.SourceApplication = entry.SourceApplication;
                _entries.Remove(existing);
                _entries.Insert(0, existing);
            }
            else
            {
                _entries.Insert(0, entry);
            }

            ApplyRetention(settings.NormalizedRetentionLimit);
            Save();
        }
    }

    public void MarkUsed(string id, ClipboardAppSettings settings)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) return;
            entry.LastUsedAt = DateTimeOffset.Now;
            _entries.Remove(entry);
            _entries.Insert(0, entry);
            ApplyRetention(settings.NormalizedRetentionLimit);
            Save();
        }
    }

    public void EnforceRetention(ClipboardAppSettings settings)
    {
        lock (_lock)
        {
            ApplyRetention(settings.NormalizedRetentionLimit);
            Save();
        }
    }

    public void Rename(string id, string name)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) return;
            entry.CustomName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            Save();
        }
    }

    public void SetPinned(string id, bool pinned)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) return;
            entry.IsPinned = pinned;
            Save();
        }
    }

    public void Delete(string id)
    {
        lock (_lock)
        {
            _entries.RemoveAll(e => e.Id == id);
            Save();
        }
    }

    public int DeleteAll()
    {
        lock (_lock)
        {
            int count = _entries.Count;
            _entries.Clear();
            Save();
            return count;
        }
    }

    public int DeleteOlderThan(TimeSpan window)
    {
        DateTimeOffset cutoff = DateTimeOffset.Now - window;
        lock (_lock)
        {
            int before = _entries.Count;
            _entries.RemoveAll(e => !e.IsPinned && e.CreatedAt >= cutoff);
            Save();
            return before - _entries.Count;
        }
    }

    public IReadOnlyList<IReadOnlyList<ClipboardEntry>> Groups(ClipboardEntryKind? kind, string query)
    {
        var source = Search(kind, query);
        var groups = new List<List<ClipboardEntry>>();
        foreach (var entry in source)
        {
            if (groups.Count == 0 || groups[^1][0].GroupId != entry.GroupId)
                groups.Add([]);
            groups[^1].Add(entry);
        }
        return groups;
    }

    public IReadOnlyList<ClipboardEntry> Search(ClipboardEntryKind? kind, string query)
    {
        lock (_lock)
        {
            IEnumerable<ClipboardEntry> q = _entries;
            if (kind is not null)
                q = q.Where(e => e.Kind == kind.Value);

            if (!string.IsNullOrWhiteSpace(query))
            {
                string needle = query.Trim();
                q = q.Where(e =>
                    e.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                    (e.Text?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.OcrText?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    e.FilePaths.Any(p => p.Contains(needle, StringComparison.OrdinalIgnoreCase)));
            }

            return q.OrderByDescending(e => e.IsPinned)
                .ThenByDescending(e => e.LastUsedAt ?? e.CreatedAt)
                .Select(Clone)
                .ToArray();
        }
    }

    public string AllocateGroupId(ClipboardEntryKind kind, DateTimeOffset createdAt)
    {
        lock (_lock)
        {
            var latest = _entries
                .Where(e => e.Kind == kind)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();
            if (latest is not null && ShouldJoinGroup(kind, createdAt - latest.CreatedAt))
                return latest.GroupId;
            return "grp_" + createdAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // Merges text entries from the sync folder that aren't already in local history.
    // Called by the extension when the user opens Clipboard History (not by the keeper).
    public void SyncFrom(string syncFolder)
    {
        var newEntries = ClipboardSyncService.ReadNewEntries(syncFolder, GetKnownIds());
        if (newEntries.Count == 0) return;

        lock (_lock)
        {
            foreach (var entry in newEntries)
            {
                if (_entries.Any(e => e.Id == entry.Id || e.ContentHash == entry.ContentHash))
                    continue;
                // Insert in chronological position (most recent first).
                int pos = _entries.FindIndex(e => e.CreatedAt <= entry.CreatedAt);
                if (pos < 0)
                    _entries.Add(entry);
                else
                    _entries.Insert(pos, entry);
            }
            Save();
        }
    }

    private HashSet<string> GetKnownIds()
    {
        lock (_lock)
            return _entries.Select(e => e.Id).ToHashSet();
    }

    public static string BuildHash(string prefix, string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(prefix + "\n" + value));
        return Convert.ToHexString(bytes);
    }

    private void ApplyRetention(int retentionLimit)
    {
        if (retentionLimit < 0)
            return;

        var unpinned = _entries.Where(e => !e.IsPinned).Skip(retentionLimit).ToArray();
        foreach (var entry in unpinned)
            _entries.Remove(entry);
    }

    private void Load()
    {
        try
        {
            string path = ClipboardPaths.HistoryPath();
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize(json, ClipboardJsonContext.Default.ListClipboardEntry);
            if (list is null) return;
            lock (_lock)
                _entries.AddRange(list.Where(e => !string.IsNullOrWhiteSpace(e.Id)));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardStore Load failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            string path = ClipboardPaths.HistoryPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = $"{path}.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, ClipboardJsonContext.Default.ListClipboardEntry));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool ShouldJoinGroup(ClipboardEntryKind kind, TimeSpan gap)
    {
        if (kind == ClipboardEntryKind.Image)
            return gap <= TimeSpan.FromMinutes(2);
        if (kind == ClipboardEntryKind.Files)
            return gap <= TimeSpan.FromSeconds(10);
        return gap <= TimeSpan.FromSeconds(30);
    }

    private static ClipboardEntry Clone(ClipboardEntry e) => new()
    {
        Id = e.Id,
        GroupId = e.GroupId,
        Kind = e.Kind,
        CreatedAt = e.CreatedAt,
        LastUsedAt = e.LastUsedAt,
        Title = e.Title,
        CustomName = e.CustomName,
        Text = e.Text,
        OcrText = e.OcrText,
        ImagePath = e.ImagePath,
        FilePaths = [.. e.FilePaths],
        SourceApplication = e.SourceApplication,
        ContentHash = e.ContentHash,
        IsPinned = e.IsPinned,
        SourceDevice = e.SourceDevice,
    };
}
