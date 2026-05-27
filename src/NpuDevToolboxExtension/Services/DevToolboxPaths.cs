using System;
using System.IO;

namespace NpuTools.DevToolbox.Services;

internal static class DevToolboxPaths
{
    private static string AppDataDir()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "NpuTools", "DevToolbox");
    }

    internal static string SettingsPath() => Path.Combine(AppDataDir(), "settings.json");

    internal static string RecentWorkspacesPath() => Path.Combine(AppDataDir(), "recent-workspaces.json");

    internal static string CommandPaletteSettingsPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "Windows", "CommandPalette", "settings.json");
    }
}
