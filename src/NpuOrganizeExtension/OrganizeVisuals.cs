using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Organize;

internal static class OrganizeVisuals
{
    internal static readonly IconInfo Folder    = new("\uE8DA"); // FolderOpen
    internal static readonly IconInfo Camera    = new("\uE722"); // Camera
    internal static readonly IconInfo Rename    = new("\uE8AC"); // Rename
    internal static readonly IconInfo DryRun    = new("\uE8FF"); // Preview
    internal static readonly IconInfo Watcher   = new("\uE7B3"); // View
    internal static readonly IconInfo Check     = new("\uE73E"); // Accept
    internal static readonly IconInfo Warning   = new("\uE7BA"); // Warning
    internal static readonly IconInfo File      = new("\uE8A5"); // Document
    internal static readonly IconInfo Start     = new("\uE768"); // Play
    internal static readonly IconInfo Stop      = new("\uE71A"); // Stop
    internal static readonly IconInfo Refresh   = new("\uE72C"); // Refresh
    internal static readonly IconInfo Search    = new("\uE721"); // Search

    internal static Tag MutedTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(210, 210, 210, 255)),
        Background = new OptionalColor(true, new Color(74, 74, 74, 255)),
    };

    internal static Tag SuccessTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(255, 255, 255, 255)),
        Background = new OptionalColor(true, new Color(36, 120, 84, 255)),
    };

    internal static Tag WarningTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(255, 255, 255, 255)),
        Background = new OptionalColor(true, new Color(156, 106, 0, 255)),
    };
}
