using System;
using System.IO;

namespace NpuTools.Common;

public static class NpuPaths
{
    public static string GetSettingsDirectory(string extensionDirectoryName)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "NpuTools", extensionDirectoryName);
    }

    public static DirectoryInfo EnsureSettingsDirectory(string extensionDirectoryName)
    {
        return Directory.CreateDirectory(GetSettingsDirectory(extensionDirectoryName));
    }
}
