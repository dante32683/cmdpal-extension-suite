using System;

namespace NpuTools.Obsidian.Services;

internal static class ObsidianUriService
{
    public static string OpenNote(string vaultName, string relativePath)
    {
        string normalizedPath = relativePath.Replace('\\', '/');
        if (normalizedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            normalizedPath = normalizedPath[..^3];

        return $"obsidian://open?vault={Uri.EscapeDataString(vaultName)}&file={Uri.EscapeDataString(normalizedPath)}";
    }

    public static string NewNote(string vaultName, string? folder = null)
    {
        string uri = $"obsidian://new?vault={Uri.EscapeDataString(vaultName)}";
        if (!string.IsNullOrWhiteSpace(folder))
            uri += $"&path={Uri.EscapeDataString(folder.Replace('\\', '/'))}";
        return uri;
    }

    public static string DailyNote(string vaultName)
    {
        return $"obsidian://daily?vault={Uri.EscapeDataString(vaultName)}";
    }

    public static string MarkdownLink(string title, string vaultName, string relativePath)
    {
        return $"[{title}]({OpenNote(vaultName, relativePath)})";
    }
}
