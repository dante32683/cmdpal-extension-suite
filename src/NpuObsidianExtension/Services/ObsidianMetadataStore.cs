using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Shared;

namespace NpuTools.Obsidian.Services;

internal sealed class ObsidianMetadataStore
{
    private readonly object _lock = new();
    private readonly ObsidianSettingsStore _settings;
    private ObsidianVaultIndex _index = new();
    private string? _loadedVault;

    public ObsidianMetadataStore(ObsidianSettingsStore settings)
    {
        _settings = settings;
    }

    public void Apply(ObsidianNote note)
    {
        EnsureLoaded();
        lock (_lock)
        {
            string relative = note.RelativePath;
            var pin = _index.Pinned.FirstOrDefault(p => SamePath(p.Path, relative));
            if (pin is not null)
            {
                note.IsPinned = true;
                note.PinOrder = pin.PinOrder;
            }

            var recent = _index.Recent.FirstOrDefault(r => SamePath(r.Path, relative));
            if (recent is not null)
                note.LastOpenedUtc = recent.OpenedUtc;
        }
    }

    public void RecordOpened(ObsidianNote note)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _index.Recent.RemoveAll(r => SamePath(r.Path, note.RelativePath));
            _index.Recent.Insert(0, new ObsidianRecentEntry { Path = note.RelativePath, OpenedUtc = DateTimeOffset.UtcNow });
            if (_index.Recent.Count > 100)
                _index.Recent.RemoveRange(100, _index.Recent.Count - 100);
            Save();
        }
    }

    public void SetPinned(ObsidianNote note, bool pinned)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _index.Pinned.RemoveAll(p => SamePath(p.Path, note.RelativePath));
            if (pinned)
            {
                int nextOrder = _index.Pinned.Count == 0 ? 0 : _index.Pinned.Max(p => p.PinOrder) + 1;
                _index.Pinned.Add(new ObsidianPinEntry { Path = note.RelativePath, PinOrder = nextOrder });
            }

            Save();
        }
    }

    public void Prune(IReadOnlyCollection<ObsidianNote> notes)
    {
        EnsureLoaded();
        lock (_lock)
        {
            var paths = notes.Select(n => n.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int pinned = _index.Pinned.RemoveAll(p => !paths.Contains(p.Path));
            int recent = _index.Recent.RemoveAll(r => !paths.Contains(r.Path));
            if (pinned > 0 || recent > 0)
                Save();
        }
    }

    private void EnsureLoaded()
    {
        string vault = _settings.Current.VaultPath;
        lock (_lock)
        {
            if (string.Equals(_loadedVault, vault, StringComparison.OrdinalIgnoreCase))
                return;

            _loadedVault = vault;
            _index = Load();
        }
    }

    private static ObsidianVaultIndex Load()
    {
        try
        {
            string path = IndexPath();
            if (!File.Exists(path))
                return new ObsidianVaultIndex();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, ObsidianJsonContext.Default.ObsidianVaultIndex) ?? new ObsidianVaultIndex();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianMetadataStore Load failed: {ex.GetType().Name}: {ex.Message}");
            return new ObsidianVaultIndex();
        }
    }

    private void Save()
    {
        try
        {
            string path = IndexPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_index, ObsidianJsonContext.Default.ObsidianVaultIndex));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianMetadataStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string IndexPath() => Path.Combine(ObsidianPaths.SupportDir(), "vault-metadata.json");

    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
