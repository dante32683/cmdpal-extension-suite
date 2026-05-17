using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NpuOrganizeKeeper;

public sealed class StateFile
{
    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("lastHeartbeatAt")]
    public string? LastHeartbeatAt { get; set; }

    [JsonPropertyName("lastEventAt")]
    public string? LastEventAt { get; set; }

    [JsonPropertyName("lastProcessedAt")]
    public string? LastProcessedAt { get; set; }

    [JsonPropertyName("lastProcessedPath")]
    public string? LastProcessedPath { get; set; }

    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }

    [JsonPropertyName("processed")]
    public int Processed { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("errors")]
    public int Errors { get; set; }

    [JsonPropertyName("watchFolder")]
    public string? WatchFolder { get; set; }
}

public sealed class KeeperConfig
{
    [JsonPropertyName("watchFolder")]
    public string WatchFolder { get; set; } = "";

    [JsonPropertyName("fileExtensions")]
    public List<string> FileExtensions { get; set; } = [".png", ".jpg", ".jpeg", ".webp"];

    [JsonPropertyName("skipAlreadyNamed")]
    public bool SkipAlreadyNamed { get; set; } = true;

    [JsonPropertyName("maxFileSizeBytes")]
    public long? MaxFileSizeBytes { get; set; }

    [JsonPropertyName("maxSlugTokens")]
    public int MaxSlugTokens { get; set; } = 5;

    [JsonPropertyName("skipOnBattery")]
    public bool SkipOnBattery { get; set; } = true;

    [JsonPropertyName("debounceMs")]
    public int DebounceMs { get; set; } = 1500;

    [JsonPropertyName("ignorePattern")]
    public string? IgnorePattern { get; set; }
}

internal sealed class StateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public string SupportDir { get; }
    public string ConfigPath { get; }
    public string StatePath { get; }
    public string LogPath { get; }
    public string StopFlagPath { get; }

    public StateStore(string supportDir)
    {
        SupportDir = supportDir;
        Directory.CreateDirectory(supportDir);
        ConfigPath   = Path.Combine(supportDir, "config.json");
        StatePath    = Path.Combine(supportDir, "state.json");
        LogPath      = Path.Combine(supportDir, "organize.log");
        StopFlagPath = Path.Combine(supportDir, "stop.flag");
    }

    public KeeperConfig? TryLoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return null;
            string json = File.ReadAllText(ConfigPath);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<KeeperConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void SaveConfig(KeeperConfig config) => AtomicWriteJson(ConfigPath, config);

    public DateTime ConfigLastWriteUtc()
    {
        try { return File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : DateTime.MinValue; }
        catch { return DateTime.MinValue; }
    }

    public StateFile LoadState()
    {
        try
        {
            if (!File.Exists(StatePath)) return new StateFile();
            string json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<StateFile>(json, JsonOptions) ?? new StateFile();
        }
        catch { return new StateFile(); }
    }

    public void SaveState(StateFile state) => AtomicWriteJson(StatePath, state);

    public void AppendLog(string line)
    {
        try
        {
            string ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            File.AppendAllText(LogPath, $"{ts}  {line}{Environment.NewLine}");
        }
        catch { /* logging must not crash the watcher */ }
    }

    public bool IsStopRequested() => File.Exists(StopFlagPath);

    public void WriteStopFlag()
    {
        try { File.WriteAllText(StopFlagPath, string.Empty); }
        catch { /* best-effort */ }
    }

    public void ClearStopFlag()
    {
        try { if (File.Exists(StopFlagPath)) File.Delete(StopFlagPath); }
        catch { /* best-effort */ }
    }

    private static void AtomicWriteJson(string filePath, object value)
    {
        string dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        string tmp = $"{filePath}.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}.tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(tmp, filePath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }
            throw;
        }
    }

    // Uses %LOCALAPPDATA% env var to get the real path — SpecialFolder.LocalApplicationData
    // is redirected inside MSIX package containers.
    public static string DefaultSupportDir()
    {
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "NpuOrganize");
    }
}
