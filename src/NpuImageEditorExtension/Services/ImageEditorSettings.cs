using System;
using System.IO;

namespace NpuTools.ImageEditor.Services;

internal sealed class ImageEditorSettings
{
    public int DefaultScaleFactor { get; set; } = 2;
    public bool AutoOpenResult { get; set; }
    public bool OcrAutoCopyText { get; set; }
    public bool OcrAutoOpenTextFile { get; set; }
}

internal static class ImageEditorPaths
{
    internal static string SupportDir()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NpuImageEditor");
        Directory.CreateDirectory(dir);
        return dir;
    }

    internal static string SettingsJsonPath() => Path.Combine(SupportDir(), "settings.json");
}
