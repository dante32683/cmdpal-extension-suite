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
    private DateTime _lastWriteTime = DateTime.MinValue;

    // Raised after every successful mutation. Subscribers (typically pages) should call
    // RaiseItemsChanged() in the handler so the host re-calls GetItems().
    // The event fires outside _lock to avoid holding the lock across subscriber callbacks.
    public event Action? Changed;

    public ClipboardStore()
    {
        Load();
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                EnsureFresh();
                return _entries.Count;
            }
        }
    }

    public IReadOnlyList<ClipboardEntry> Snapshot()
    {
        lock (_lock)
        {
            EnsureFresh();
            return _entries.Select(Clone).ToArray();
        }
    }

    public ClipboardEntry? Get(string id)
    {
        lock (_lock)
        {
            EnsureFresh();
            return _entries.FirstOrDefault(e => e.Id == id) is { } entry ? Clone(entry) : null;
        }
    }

    public void AddOrPromote(ClipboardEntry entry, ClipboardAppSettings settings)
    {
        lock (_lock)
        {
            EnsureFresh();
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
        Changed?.Invoke();
    }

    public void MarkUsed(string id, ClipboardAppSettings settings)
    {
        bool mutated;
        lock (_lock)
        {
            EnsureFresh();
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) { mutated = false; return; }
            entry.LastUsedAt = DateTimeOffset.Now;
            _entries.Remove(entry);
            _entries.Insert(0, entry);
            ApplyRetention(settings.NormalizedRetentionLimit);
            Save();
            mutated = true;
        }
        if (mutated) Changed?.Invoke();
    }

    public void EnforceRetention(ClipboardAppSettings settings)
    {
        bool removed;
        lock (_lock)
        {
            EnsureFresh();
            int before = _entries.Count;
            ApplyRetention(settings.NormalizedRetentionLimit);
            removed = _entries.Count != before;
            if (removed) Save();
        }
        if (removed) Changed?.Invoke();
    }

    public void Rename(string id, string name)
    {
        bool mutated;
        lock (_lock)
        {
            EnsureFresh();
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) { mutated = false; return; }
            entry.CustomName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            Save();
            mutated = true;
        }
        if (mutated) Changed?.Invoke();
    }

    public void SetPinned(string id, bool pinned)
    {
        bool mutated;
        lock (_lock)
        {
            EnsureFresh();
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) { mutated = false; return; }
            entry.IsPinned = pinned;
            Save();
            mutated = true;
        }
        if (mutated) Changed?.Invoke();
    }

    public void Delete(string id)
    {
        lock (_lock)
        {
            EnsureFresh();
            _entries.RemoveAll(e => e.Id == id);
            Save();
        }
        Changed?.Invoke();
    }

    public int DeleteAll()
    {
        int count;
        lock (_lock)
        {
            EnsureFresh();
            count = _entries.Count;
            _entries.Clear();
            Save();
        }
        Changed?.Invoke();
        return count;
    }

    public int DeleteOlderThan(TimeSpan window)
    {
        DateTimeOffset cutoff = DateTimeOffset.Now - window;
        int removed;
        lock (_lock)
        {
            EnsureFresh();
            int before = _entries.Count;
            _entries.RemoveAll(e => !e.IsPinned && e.CreatedAt >= cutoff);
            removed = before - _entries.Count;
            Save();
        }
        if (removed > 0) Changed?.Invoke();
        return removed;
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
            EnsureFresh();
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
            EnsureFresh();
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
    // Settings are required so cross-device sync respects the user's secret-pattern filter
    // — a secret on device A must not be allowed to land in local history on device B just
    // because the capture path was the sync folder rather than the local clipboard.
    public void SyncFrom(string syncFolder, ClipboardAppSettings settings)
    {
        var newEntries = ClipboardSyncService.ReadNewEntries(syncFolder, GetKnownIds());
        if (newEntries.Count == 0) return;

        var matcher = new SecretPatternMatcher(settings);

        bool merged;
        lock (_lock)
        {
            EnsureFresh();
            merged = false;
            foreach (var entry in newEntries)
            {
                if (_entries.Any(e => e.Id == entry.Id || e.ContentHash == entry.ContentHash))
                    continue;
                if (matcher.Match(entry.Text) is { } matched)
                {
                    Debug.WriteLine($"ClipboardStore.SyncFrom dropped '{entry.Id}': matched secret pattern: {matched}");
                    continue;
                }
                // Insert in chronological position (most recent first).
                int pos = _entries.FindIndex(e => e.CreatedAt <= entry.CreatedAt);
                if (pos < 0)
                    _entries.Add(entry);
                else
                    _entries.Insert(pos, entry);
                merged = true;
            }
            if (merged) Save();
        }
        if (merged) Changed?.Invoke();
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
        lock (_lock)
        {
            try
            {
                string path = ClipboardPaths.HistoryPath();
                if (File.Exists(path))
                {
                    _lastWriteTime = File.GetLastWriteTimeUtc(path);
                    string json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize(json, ClipboardJsonContext.Default.ListClipboardEntry);
                    if (list is not null)
                    {
                        _entries.Clear();
                        _entries.AddRange(list.Where(e => !string.IsNullOrWhiteSpace(e.Id)));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ClipboardStore Load failed: {ex.GetType().Name}: {ex.Message}");
            }
            _entries.Clear();
            _lastWriteTime = DateTime.MinValue;
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
            // Force a fresh mtime so EnsureFresh reliably detects this write.
            // File.WriteAllText + File.Move can land on the same NTFS mtime as the previous
            // file (same millisecond) which would make the change invisible to the
            // writeTime != _lastWriteTime check in EnsureFresh.
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            _lastWriteTime = File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void EnsureFresh()
    {
        try
        {
            string path = ClipboardPaths.HistoryPath();
            if (!File.Exists(path))
            {
                if (_entries.Count > 0)
                {
                    _entries.Clear();
                    _lastWriteTime = DateTime.MinValue;
                }
                return;
            }

            DateTime writeTime = File.GetLastWriteTimeUtc(path);
            if (writeTime != _lastWriteTime)
            {
                Load();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardStore EnsureFresh failed: {ex.GetType().Name}: {ex.Message}");
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
