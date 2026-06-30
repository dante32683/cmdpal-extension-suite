using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NpuTools.Clipboard.Data;

public sealed class ClipboardSettingsStore
{
    private readonly object _lock = new();
    private ClipboardAppSettings _settings = new();

    public ClipboardSettingsStore()
    {
        Load();
    }

    public ClipboardAppSettings Current
    {
        get { lock (_lock) return Clone(_settings); }
    }

    public void Update(Action<ClipboardAppSettings> update)
    {
        lock (_lock)
        {
            update(_settings);
            Normalize(_settings);
            Save();
        }
    }

    public void Reload()
    {
        lock (_lock)
            Load();
    }

    private void Load()
    {
        try
        {
            string path = ClipboardPaths.SettingsPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _settings = JsonSerializer.Deserialize(json, ClipboardJsonContext.Default.ClipboardAppSettings) ?? new ClipboardAppSettings();
            }
            Normalize(_settings);
            Save();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardSettingsStore Load failed: {ex.GetType().Name}: {ex.Message}");
            _settings = new ClipboardAppSettings();
        }
    }

    private void Save()
    {
        try
        {
            string path = ClipboardPaths.SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = $"{path}.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_settings, ClipboardJsonContext.Default.ClipboardAppSettings));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ClipboardSettingsStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Normalize(ClipboardAppSettings settings)
    {
        if (settings.RetentionLimit == 0)
            settings.RetentionLimit = ClipboardAppSettings.DefaultRetentionLimit;
        if (settings.PasteDelayMs < 50)
            settings.PasteDelayMs = 250;
        settings.DisabledApplicationNames.RemoveAll(string.IsNullOrWhiteSpace);
        if (settings.SecretPatterns is null || settings.SecretPatterns.Count == 0)
            settings.SecretPatterns = DefaultSecretPatterns.Copy();
        settings.SecretPatterns.RemoveAll(p => string.IsNullOrWhiteSpace(p.Regex));
    }

    private static ClipboardAppSettings Clone(ClipboardAppSettings s) => new()
    {
        PrimaryAction = s.PrimaryAction,
        RetentionLimit = s.RetentionLimit,
        DisabledApplicationNames = [.. s.DisabledApplicationNames],
        PasteDelayMs = s.PasteDelayMs,
        RecorderEnabled = s.RecorderEnabled,
        PreviewMode = s.PreviewMode,
        SyncFolder = s.SyncFolder,
        SecretDetectionEnabled = s.SecretDetectionEnabled,
        SecretPatterns = s.SecretPatterns.Select(p => new SecretPattern { Name = p.Name, Regex = p.Regex }).ToList(),
    };
}
