using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NpuTools.Notes.Models;
using NpuTools.Notes.Shared;

namespace NpuTools.Notes.Services;

internal sealed class NotesIndexStore
{
    private readonly object _lock = new();
    private readonly NotesSettingsStore _settings;
    private NotesIndex _index = new();
    private string? _loadedRoot;

    public NotesIndexStore(NotesSettingsStore settings)
    {
        _settings = settings;
    }

    public void Apply(NoteEntry entry)
    {
        EnsureLoaded();
        lock (_lock)
        {
            string relative = entry.RelativePath;
            var pin = _index.Pinned.FirstOrDefault(p => SamePath(p.Path, relative));
            if (pin is not null)
            {
                entry.IsPinned = true;
                entry.PinOrder = pin.PinOrder;
            }

            var recent = _index.Recent.FirstOrDefault(r => SamePath(r.Path, relative));
            if (recent is not null)
                entry.LastOpenedUtc = recent.OpenedUtc;
        }
    }

    public void RecordOpened(NoteEntry entry)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _index.Recent.RemoveAll(r => SamePath(r.Path, entry.RelativePath));
            _index.Recent.Insert(0, new NotesRecentEntry { Path = entry.RelativePath, OpenedUtc = DateTimeOffset.UtcNow });
            if (_index.Recent.Count > 100)
                _index.Recent.RemoveRange(100, _index.Recent.Count - 100);
            Save();
        }
    }

    public void SetPinned(NoteEntry entry, bool pinned)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _index.Pinned.RemoveAll(p => SamePath(p.Path, entry.RelativePath));
            if (pinned)
            {
                int nextOrder = _index.Pinned.Count == 0 ? 0 : _index.Pinned.Max(p => p.PinOrder) + 1;
                _index.Pinned.Add(new NotesPinEntry { Path = entry.RelativePath, PinOrder = nextOrder });
            }

            Save();
        }
    }

    public void Remove(NoteEntry entry)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _index.Pinned.RemoveAll(p => SamePath(p.Path, entry.RelativePath));
            _index.Recent.RemoveAll(r => SamePath(r.Path, entry.RelativePath));
            Save();
        }
    }

    public void Remap(NoteEntry oldEntry, NoteEntry newEntry)
    {
        EnsureLoaded();
        lock (_lock)
        {
            bool changed = false;
            foreach (var pin in _index.Pinned)
            {
                if (SamePath(pin.Path, oldEntry.RelativePath))
                {
                    pin.Path = newEntry.RelativePath;
                    changed = true;
                }
            }

            foreach (var recent in _index.Recent)
            {
                if (SamePath(recent.Path, oldEntry.RelativePath))
                {
                    recent.Path = newEntry.RelativePath;
                    changed = true;
                }
            }

            if (changed)
                Save();
        }
    }

    public void Prune(IReadOnlyCollection<NoteEntry> entries)
    {
        EnsureLoaded();
        lock (_lock)
        {
            var paths = entries.Select(e => e.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int pinned = _index.Pinned.RemoveAll(p => !paths.Contains(p.Path));
            int recent = _index.Recent.RemoveAll(r => !paths.Contains(r.Path));
            if (pinned > 0 || recent > 0)
                Save();
        }
    }

    private void EnsureLoaded()
    {
        string root = _settings.Current.NotesRoot;
        lock (_lock)
        {
            if (string.Equals(_loadedRoot, root, StringComparison.OrdinalIgnoreCase))
                return;

            _loadedRoot = root;
            _index = Load(root);
        }
    }

    private static NotesIndex Load(string root)
    {
        try
        {
            string path = IndexPath(root);
            if (!File.Exists(path))
                return new NotesIndex();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, NotesJsonContext.Default.NotesIndex) ?? new NotesIndex();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesIndexStore Load failed: {ex.GetType().Name}: {ex.Message}");
            return new NotesIndex();
        }
    }

    private void Save()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_loadedRoot))
                return;

            Directory.CreateDirectory(_loadedRoot);
            string path = IndexPath(_loadedRoot);
            string tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_index, NotesJsonContext.Default.NotesIndex));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesIndexStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string IndexPath(string root) => Path.Combine(root, ".notes-index.json");

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
