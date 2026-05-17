using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NpuTools.Organize.Models;

namespace NpuTools.Organize.Services;

internal sealed partial class ScreenshotIndexService
{
    private static readonly string IndexPath = Path.Combine(
        Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NpuOrganize", "index.json");

    private readonly object _lock = new();
    private readonly Dictionary<string, ScreenshotIndexEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public ScreenshotIndexService()
    {
        Load();
    }

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
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
            _entries[filePath] = entry;
            Save();
        }
    }

    public void UpdatePath(string oldPath, string newPath)
    {
        lock (_lock)
        {
            if (_entries.Remove(oldPath, out var entry))
            {
                entry.FilePath = newPath;
                _entries[newPath] = entry;
                Save();
            }
        }
    }

    public bool IsIndexed(string filePath)
    {
        lock (_lock)
            return _entries.ContainsKey(filePath);
    }

    public IReadOnlyList<ScreenshotIndexEntry> Search(string query)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(query))
                return [.. _entries.Values];

            var results = new List<ScreenshotIndexEntry>();
            string q = query.Trim();
            foreach (var entry in _entries.Values)
            {
                if (entry.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    entry.OcrText.Contains(q, StringComparison.OrdinalIgnoreCase))
                    results.Add(entry);
            }
            return results;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(IndexPath)) return;
            string json = File.ReadAllText(IndexPath);
            var list = JsonSerializer.Deserialize(json, IndexJsonContext.Default.ListScreenshotIndexEntry);
            if (list is null) return;
            lock (_lock)
            {
                foreach (var entry in list)
                    _entries[entry.FilePath] = entry;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ScreenshotIndexService Load failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(IndexPath)!);
            var snapshot = new List<ScreenshotIndexEntry>(_entries.Values);
            string json = JsonSerializer.Serialize(snapshot, IndexJsonContext.Default.ListScreenshotIndexEntry);
            File.WriteAllText(IndexPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ScreenshotIndexService Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [JsonSerializable(typeof(List<ScreenshotIndexEntry>))]
    [JsonSourceGenerationOptions(WriteIndented = false)]
    private sealed partial class IndexJsonContext : JsonSerializerContext { }

}
