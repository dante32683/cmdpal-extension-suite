using System;
using System.IO;

namespace NpuTools.Notes.Services;

internal static class NotesPaths
{
    public static string SupportDir()
    {
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "NpuNotes");
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
