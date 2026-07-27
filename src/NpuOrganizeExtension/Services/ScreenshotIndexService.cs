using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NpuTools.Organize.Models;

namespace NpuTools.Organize.Services;

internal sealed partial class ScreenshotIndexService : IDisposable
{
    private static readonly string IndexPath = Path.Combine(
        Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NpuOrganize", "index.json");

    private readonly object _lock = new();
    private readonly Dictionary<string, ScreenshotIndexEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource _saveCancellationTokenSource = new();
    private Task? _saveTask;
    private DateTime _lastWriteTime = DateTime.MinValue;

    public ScreenshotIndexService()
    {
        Load();
    }

    public void Dispose()
    {
        _saveCancellationTokenSource.Cancel();
        try { _saveTask?.GetAwaiter().GetResult(); } catch { /* cancellation or a logged save failure */ }
        try { SaveAsync(CancellationToken.None).GetAwaiter().GetResult(); } catch { /* best effort final flush */ }
        _saveCancellationTokenSource.Dispose();
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

    public void Upsert(string filePath, string description, string ocrText)
    {
        var entry = new ScreenshotIndexEntry
        {
            FilePath    = filePath,
            Description = description,
            OcrText     = ocrText,
            IndexedAt   = DateTimeOffset.Now,
        };
        lock (_lock)
        {
            EnsureFresh();
            _entries[filePath] = entry;
            QueueSave();
        }
    }

    public void UpdatePath(string oldPath, string newPath)
    {
        lock (_lock)
        {
            EnsureFresh();
            if (_entries.Remove(oldPath, out var entry))
            {
                entry.FilePath = newPath;
                _entries[newPath] = entry;
                QueueSave();
            }
        }
    }

    public bool IsIndexed(string filePath)
    {
        lock (_lock)
        {
            EnsureFresh();
            return _entries.ContainsKey(filePath);
        }
    }

    public IReadOnlyList<ScreenshotIndexEntry> Recent(int maxCount = 20)
    {
        lock (_lock)
        {
            EnsureFresh();
            var all = new List<ScreenshotIndexEntry>(_entries.Values);
            all.Sort(static (a, b) => b.IndexedAt.CompareTo(a.IndexedAt));
            return all.Count <= maxCount ? all : all.GetRange(0, maxCount);
        }
    }

    public IReadOnlyList<ScreenshotIndexEntry> Search(string query)
    {
        lock (_lock)
        {
            EnsureFresh();
            if (string.IsNullOrWhiteSpace(query))
                return [.. _entries.Values];

            string q = query.Trim();
            var scored = new List<(ScreenshotIndexEntry Entry, int Score)>();
            foreach (var entry in _entries.Values)
            {
                int score = ScoreEntry(entry, q);
                if (score > 0)
                    scored.Add((entry, score));
            }

            scored.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score);
                return cmp != 0 ? cmp : b.Entry.IndexedAt.CompareTo(a.Entry.IndexedAt);
            });

            var results = new List<ScreenshotIndexEntry>(scored.Count);
            foreach (var (entry, _) in scored)
                results.Add(entry);
            return results;
        }
    }

    private static int ScoreEntry(ScreenshotIndexEntry entry, string q)
    {
        int score = 0;

        bool inDesc = entry.Description.Contains(q, StringComparison.OrdinalIgnoreCase);
        bool inOcr  = entry.OcrText.Contains(q, StringComparison.OrdinalIgnoreCase);

        if (inDesc) score += 3;
        if (inOcr)  score += 2;

        if (score > 0 && IsWholeWord(entry.Description + " " + entry.OcrText, q))
            score += 1;

        return score;
    }

    private static bool IsWholeWord(string text, string q)
    {
        int idx = text.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            bool leftBound  = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            bool rightBound = idx + q.Length >= text.Length || !char.IsLetterOrDigit(text[idx + q.Length]);
            if (leftBound && rightBound)
                return true;
            idx = text.IndexOf(q, idx + 1, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(IndexPath))
                {
                    _lastWriteTime = File.GetLastWriteTimeUtc(IndexPath);
                    string json = File.ReadAllText(IndexPath);
                    var list = JsonSerializer.Deserialize(json, IndexJsonContext.Default.ListScreenshotIndexEntry);
                    if (list is not null)
                    {
                        _entries.Clear();
                        foreach (var entry in list)
                            _entries[entry.FilePath] = entry;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ScreenshotIndexService Load failed: {ex.GetType().Name}: {ex.Message}");
            }
            _entries.Clear();
            _lastWriteTime = DateTime.MinValue;
        }
    }

    private void QueueSave()
    {
        _saveCancellationTokenSource.Cancel();
        _saveCancellationTokenSource = new CancellationTokenSource();
        var token = _saveCancellationTokenSource.Token;

        _saveTask = SaveAfterDelayAsync(token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), token).ConfigureAwait(false);
            await SaveAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        using var mutex = new Mutex(false, IndexMutexName());
        try
        {
            try { mutex.WaitOne(); } catch (AbandonedMutexException) { }
            Directory.CreateDirectory(Path.GetDirectoryName(IndexPath)!);
            List<ScreenshotIndexEntry> snapshot;
            lock (_lock)
            {
                snapshot = new List<ScreenshotIndexEntry>(_entries.Values);
            }
            string json = JsonSerializer.Serialize(snapshot, IndexJsonContext.Default.ListScreenshotIndexEntry);
            string tmp = $"{IndexPath}.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}.tmp";
            await File.WriteAllTextAsync(tmp, json, cancellationToken).ConfigureAwait(false);
            File.Move(tmp, IndexPath, overwrite: true);
            lock (_lock)
            {
                _lastWriteTime = File.GetLastWriteTimeUtc(IndexPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ScreenshotIndexService Save failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try { mutex.ReleaseMutex(); } catch { }
        }
    }

    private static string IndexMutexName()
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(IndexPath));
        return $"Local\\NpuOrganizeIndex-{Convert.ToHexString(hash)[..24]}";
    }

    private void EnsureFresh()
    {
        try
        {
            if (!File.Exists(IndexPath))
            {
                if (_entries.Count > 0)
                {
                    _entries.Clear();
                    _lastWriteTime = DateTime.MinValue;
                }
                return;
            }

            DateTime writeTime = File.GetLastWriteTimeUtc(IndexPath);
            if (writeTime != _lastWriteTime)
            {
                Load();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ScreenshotIndexService EnsureFresh failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [JsonSerializable(typeof(List<ScreenshotIndexEntry>))]
    [JsonSourceGenerationOptions(WriteIndented = false)]
    private sealed partial class IndexJsonContext : JsonSerializerContext { }

}
