using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NpuTools.DevToolbox.Shared;

namespace NpuTools.DevToolbox.Services;

internal sealed class RecentWorkspacesStore
{
    private const int MaxCount = 20;
    private readonly object _lock = new();
    private List<string> _paths = [];

    public RecentWorkspacesStore()
    {
        Load();
    }

    public IReadOnlyList<string> GetAll()
    {
        lock (_lock)
            return [.. _paths];
    }

    public void Add(string path)
    {
        string normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        lock (_lock)
        {
            _paths.RemoveAll(p => string.Equals(p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalized, StringComparison.OrdinalIgnoreCase));
            _paths.Insert(0, normalized);
            if (_paths.Count > MaxCount)
                _paths.RemoveRange(MaxCount, _paths.Count - MaxCount);
            Save();
        }
    }

    public void Remove(string path)
    {
        string normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        lock (_lock)
        {
            _paths.RemoveAll(p => string.Equals(p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalized, StringComparison.OrdinalIgnoreCase));
            Save();
        }
    }

    private void Load()
    {
        try
        {
            string path = DevToolboxPaths.RecentWorkspacesPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _paths = JsonSerializer.Deserialize(json, DevToolboxJsonContext.Default.ListString) ?? [];
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RecentWorkspacesStore Load failed: {ex.GetType().Name}: {ex.Message}");
            _paths = [];
        }
    }

    private void Save()
    {
        try
        {
            string path = DevToolboxPaths.RecentWorkspacesPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_paths, DevToolboxJsonContext.Default.ListString));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RecentWorkspacesStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
