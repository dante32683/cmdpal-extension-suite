using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Shared;

namespace NpuTools.Obsidian.Services;

internal sealed class ObsidianSettingsStore
{
    private readonly object _lock = new();
    private ObsidianVaultSettings _settings = new();

    public ObsidianSettingsStore()
    {
        Load();
    }

    public ObsidianVaultSettings Current
    {
        get
        {
            lock (_lock)
                return Clone(_settings);
        }
    }

    public void Update(Action<ObsidianVaultSettings> update)
    {
        lock (_lock)
        {
            update(_settings);
            Normalize(_settings);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            string path = ObsidianPaths.SettingsPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _settings = JsonSerializer.Deserialize(json, ObsidianJsonContext.Default.ObsidianVaultSettings) ?? new ObsidianVaultSettings();
            }

            Normalize(_settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianSettingsStore Load failed: {ex.GetType().Name}: {ex.Message}");
            _settings = new ObsidianVaultSettings();
        }
    }

    private void Save()
    {
        try
        {
            string path = ObsidianPaths.SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_settings, ObsidianJsonContext.Default.ObsidianVaultSettings));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianSettingsStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Normalize(ObsidianVaultSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.VaultPath))
            settings.VaultPath = Environment.ExpandEnvironmentVariables(settings.VaultPath.Trim());

        if (string.IsNullOrWhiteSpace(settings.VaultName) && !string.IsNullOrWhiteSpace(settings.VaultPath))
            settings.VaultName = Path.GetFileName(settings.VaultPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (settings.MaxRecentNotes is < 5 or > 100)
            settings.MaxRecentNotes = ObsidianVaultSettings.DefaultMaxRecentNotes;

        if (settings.MaxSearchResults is < 10 or > 200)
            settings.MaxSearchResults = ObsidianVaultSettings.DefaultMaxSearchResults;
    }

    private static ObsidianVaultSettings Clone(ObsidianVaultSettings s) => new()
    {
        VaultPath = s.VaultPath,
        VaultName = s.VaultName,
        DailyNotesFolder = s.DailyNotesFolder,
        DefaultNewNoteFolder = s.DefaultNewNoteFolder,
        OpenAfterCreate = s.OpenAfterCreate,
        MaxRecentNotes = s.MaxRecentNotes,
        MaxSearchResults = s.MaxSearchResults,
    };
}
