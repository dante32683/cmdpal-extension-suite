using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

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
        List<string> candidates = [];
        bool saved = Mutate(() =>
        {
            var existing = _entries.FirstOrDefault(e => e.ContentHash == entry.ContentHash);
            if (existing is not null)
            {
                if (!string.IsNullOrWhiteSpace(existing.ImagePath) &&
                    !string.Equals(existing.ImagePath, entry.ImagePath, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(existing.ImagePath);
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

            candidates.AddRange(ApplyRetention(settings.NormalizedRetentionLimit));
            return Save();
        });
        if (!saved) return;
        CleanupUnreferencedBlobs(candidates);
        Changed?.Invoke();
    }

    public void MarkUsed(string id, ClipboardAppSettings settings)
    {
        List<string> candidates = [];
        bool mutated = Mutate(() =>
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) return false;
            entry.LastUsedAt = DateTimeOffset.Now;
            _entries.Remove(entry);
            _entries.Insert(0, entry);
            candidates.AddRange(ApplyRetention(settings.NormalizedRetentionLimit));
            return Save();
        });
        if (mutated)
        {
            CleanupUnreferencedBlobs(candidates);
            Changed?.Invoke();
        }
    }

    public void EnforceRetention(ClipboardAppSettings settings)
    {
        List<string> candidates = [];
        bool removed = Mutate(() =>
        {
            int before = _entries.Count;
            candidates.AddRange(ApplyRetention(settings.NormalizedRetentionLimit));
            return _entries.Count != before && Save();
        });
        if (removed)
        {
            CleanupUnreferencedBlobs(candidates);
            Changed?.Invoke();
        }
    }

    public void Rename(string id, string name)
    {
        bool mutated = Mutate(() =>
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) return false;
            entry.CustomName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            return Save();
        });
        if (mutated) Changed?.Invoke();
    }

    public void SetPinned(string id, bool pinned)
    {
        bool mutated = Mutate(() =>
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) return false;
            entry.IsPinned = pinned;
            return Save();
        });
        if (mutated) Changed?.Invoke();
    }

    public void Delete(string id)
    {
        List<string> candidates = [];
        bool saved = Mutate(() =>
        {
            candidates.AddRange(_entries.Where(e => e.Id == id).Select(e => e.ImagePath).OfType<string>());
            int removed = _entries.RemoveAll(e => e.Id == id);
            return removed > 0 && Save();
        });
        if (!saved) return;
        CleanupUnreferencedBlobs(candidates);
        Changed?.Invoke();
    }

    public int DeleteAll()
    {
        int count = 0;
        List<string> candidates = [];
        bool saved = Mutate(() =>
        {
            count = _entries.Count;
            candidates.AddRange(_entries.Select(e => e.ImagePath).OfType<string>());
            _entries.Clear();
            return Save();
        });
        if (!saved) return 0;
        CleanupUnreferencedBlobs(candidates);
        Changed?.Invoke();
        return count;
    }

    public int DeleteWithinLast(TimeSpan window)
    {
        DateTimeOffset cutoff = DateTimeOffset.Now - window;
        List<string> candidates = [];
        int removed = Mutate(() =>
        {
            int before = _entries.Count;
            candidates.AddRange(_entries.Where(e => !e.IsPinned && e.CreatedAt >= cutoff).Select(e => e.ImagePath).OfType<string>());
            _entries.RemoveAll(e => !e.IsPinned && e.CreatedAt >= cutoff);
            int removedNow = before - _entries.Count;
            return removedNow > 0 && Save() ? removedNow : 0;
        });
        if (removed > 0)
        {
            CleanupUnreferencedBlobs(candidates);
            Changed?.Invoke();
        }
        return removed;
    }

    [Obsolete("Use DeleteWithinLast; this method deletes entries from the recent window.")]
    public int DeleteOlderThan(TimeSpan window) => DeleteWithinLast(window);

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
        var matcher = new SecretPatternMatcher(settings);
        bool merged = Mutate(() =>
        {
            var newEntries = ClipboardSyncService.ReadNewEntriesWithPaths(
                syncFolder,
                _entries.Select(e => e.Id).ToHashSet());
            if (newEntries.Count == 0)
                return false;

            bool changed = false;
            foreach (var item in newEntries)
            {
                var entry = item.Entry;
                if (_entries.Any(e => e.Id == entry.Id || e.ContentHash == entry.ContentHash))
                    continue;
                if (matcher.Match(entry.Text) is { } matched)
                {
                    Debug.WriteLine($"ClipboardStore.SyncFrom dropped '{entry.Id}': matched secret pattern: {matched}");
                    TryDeleteSyncSource(item.SourcePath, syncFolder);
                    continue;
                }
                // Insert in chronological position (most recent first).
                int pos = _entries.FindIndex(e => e.CreatedAt <= entry.CreatedAt);
                if (pos < 0)
                    _entries.Add(entry);
                else
                    _entries.Insert(pos, entry);
                changed = true;
            }
            if (changed)
            {
                ApplyRetention(settings.NormalizedRetentionLimit);
                return Save();
            }
            return false;
        });
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

    private List<string> ApplyRetention(int retentionLimit)
    {
        if (retentionLimit < 0)
            return [];

        var unpinned = _entries.Where(e => !e.IsPinned).Skip(retentionLimit).ToArray();
        foreach (var entry in unpinned)
            _entries.Remove(entry);
        return unpinned.Select(e => e.ImagePath).OfType<string>().ToList();
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

    private bool Save()
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
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardStore Save failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private T Mutate<T>(Func<T> mutation)
    {
        using var mutex = new Mutex(false, HistoryMutexName());
        try
        {
            mutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            // The abandoned owner has exited; the mutex is acquired and the file
            // will be reloaded below before applying this mutation.
        }

        try
        {
            lock (_lock)
            {
                EnsureFresh();
                return mutation();
            }
        }
        catch
        {
            try { Load(); } catch { }
            throw;
        }
        finally
        {
            // A release failure must never replace the exception that caused it.
            try { mutex.ReleaseMutex(); } catch { }
        }
    }

    private void CleanupUnreferencedBlobs(IEnumerable<string> candidates)
    {
        using var mutex = new Mutex(false, HistoryMutexName());
        try { mutex.WaitOne(); } catch (AbandonedMutexException) { }
        try
        {
            lock (_lock)
            {
                EnsureFresh();
                string root = Path.GetFullPath(ClipboardPaths.BlobDir()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                HashSet<string> referenced = _entries.Select(e => e.ImagePath).OfType<string>()
                    .Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (string candidate in candidates.Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    try
                    {
                        string full = Path.GetFullPath(candidate);
                        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !referenced.Contains(full))
                            File.Delete(full);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ClipboardStore blob cleanup failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }
        finally
        {
            try { mutex.ReleaseMutex(); } catch { }
        }
    }

    private static void TryDeleteSyncSource(string sourcePath, string? syncFolder)
    {
        if (string.IsNullOrWhiteSpace(syncFolder)) return;
        try
        {
            string root = Path.GetFullPath(Path.Combine(syncFolder, "clipboard-sync"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(sourcePath);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                File.Delete(full);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardStore sync-secret cleanup failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string HistoryMutexName() =>
        $"Local\\NpuClipboardHistory-{BuildHash("mutex", ClipboardPaths.HistoryPath())[..24]}";

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
