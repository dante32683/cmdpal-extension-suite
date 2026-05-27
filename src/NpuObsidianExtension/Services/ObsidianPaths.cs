using System;
using System.IO;

namespace NpuTools.Obsidian.Services;

internal static class ObsidianPaths
{
    public static string SupportDir()
    {
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "NpuObsidian");
    }

    public static string SettingsPath()
    {
        Directory.CreateDirectory(SupportDir());
        return Path.Combine(SupportDir(), "settings.json");
    }

    public static string CommandPaletteSettingsPath()
    {
        Directory.CreateDirectory(SupportDir());
        return Path.Combine(SupportDir(), "command-palette.settings.json");
    }
}
