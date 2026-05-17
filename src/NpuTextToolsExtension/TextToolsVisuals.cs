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

    internal static Tag MutedTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(210, 210, 210, 255)),
        Background = new OptionalColor(true, new Color(74, 74, 74, 255)),
    };
}
