using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NpuTools.DevToolbox.Models;
using NpuTools.DevToolbox.Shared;

namespace NpuTools.DevToolbox.Services;

internal sealed class DevToolboxSettingsStore
{
    private readonly object _lock = new();
    private DevToolboxSettings _settings = new();

    public DevToolboxSettingsStore()
    {
        Load();
    }

    public DevToolboxSettings Current
    {
        get
        {
            lock (_lock)
                return Clone(_settings);
        }
    }

    public void Update(Action<DevToolboxSettings> update)
    {
        lock (_lock)
        {
            update(_settings);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            string path = DevToolboxPaths.SettingsPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _settings = JsonSerializer.Deserialize(json, DevToolboxJsonContext.Default.DevToolboxSettings) ?? new DevToolboxSettings();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DevToolboxSettingsStore Load failed: {ex.GetType().Name}: {ex.Message}");
            _settings = new DevToolboxSettings();
        }
    }

    private void Save()
    {
        try
        {
            string path = DevToolboxPaths.SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_settings, DevToolboxJsonContext.Default.DevToolboxSettings));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DevToolboxSettingsStore Save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static DevToolboxSettings Clone(DevToolboxSettings s) => new()
    {
        PreferredTerminal = s.PreferredTerminal,
        PreferredIde = s.PreferredIde,
        CustomTerminalExe = s.CustomTerminalExe,
        CustomIdeExe = s.CustomIdeExe,
    };
}
