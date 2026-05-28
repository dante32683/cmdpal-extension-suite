using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.TextTools;

internal static class TextToolsVisuals
{
    internal static readonly IconInfo Pen   = new("\uE8D2"); // Edit
    internal static readonly IconInfo Check = new("\uE73E"); // Checkmark
    internal static readonly IconInfo Copy  = new("\uE8C8"); // Copy
    internal static readonly IconInfo Hub   = new("\uE8FD"); // List
    internal static readonly IconInfo Phi   = new("\uE945"); // Sparkle/AI

    private static readonly Color GreenColor = new() { R = 108, G = 203, B = 95, A = 255 };
    private static readonly Color RedColor   = new() { R = 232, G = 84, B = 84, A = 255 };

    internal static Tag StatusTag(string text) => new(text)
    {
        Foreground = new OptionalColor { HasValue = true, Color = GreenColor },
    };

    internal static Tag CriticalTag(string text) => new(text)
    {
        Foreground = new OptionalColor { HasValue = true, Color = RedColor },
    };

    internal static Tag MutedTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(210, 210, 210, 255)),
        Background = new OptionalColor(true, new Color(74, 74, 74, 255)),
    };
}
