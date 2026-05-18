using System;
using System.IO;

namespace NpuTools.Clipboard.Data;

public static class ClipboardPaths
{
    public static string SupportDir()
    {
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "NpuClipboard");
    }

    public static string HistoryPath() => Path.Combine(SupportDir(), "history.json");
    public static string SettingsPath() => Path.Combine(SupportDir(), "settings.json");
    public static string StatePath() => Path.Combine(SupportDir(), "state.json");
    public static string StopFlagPath() => Path.Combine(SupportDir(), "stop.flag");
    public static string LogPath() => Path.Combine(SupportDir(), "clipboard.log");
    public static string BlobDir() => Path.Combine(SupportDir(), "blobs");
}
