using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Obsidian;

internal static class ObsidianVisuals
{
    internal static readonly IconInfo Hub      = new("");
    internal static readonly IconInfo Search   = new("");
    internal static readonly IconInfo Note     = new("");
    internal static readonly IconInfo Add      = new("");
    internal static readonly IconInfo Open     = new("");
    internal static readonly IconInfo Edit     = new("");
    internal static readonly IconInfo Append   = new("");
    internal static readonly IconInfo Folder   = new("");
    internal static readonly IconInfo Copy     = new("");
    internal static readonly IconInfo Pin      = new("");
    internal static readonly IconInfo Delete   = new("");
    internal static readonly IconInfo Settings = new("");
    internal static readonly IconInfo Link     = new("");
    internal static readonly IconInfo Daily    = new("");
    internal static readonly IconInfo Warning  = new("");
    internal static readonly IconInfo Index    = new("");
    internal static readonly IconInfo Check    = new("");
    internal static readonly IconInfo Ai      = new("");
    internal static readonly IconInfo Related  = new("");

    private static readonly Color PurpleColor = new() { R = 124, G = 58, B = 237, A = 255 };
    private static readonly Color GreenColor  = new() { R = 108, G = 203, B = 95,  A = 255 };
    private static readonly Color RedColor    = new() { R = 255, G = 95,  B = 95,  A = 255 };
    private static readonly Color OrangeColor = new() { R = 255, G = 165, B = 0,   A = 255 };

    internal static Tag VaultTag(string text)    => new(text) { Foreground = Colored(PurpleColor) };
    internal static Tag StatusTag(string text)   => new(text) { Foreground = Colored(GreenColor)  };
    internal static Tag CriticalTag(string text) => new(text) { Foreground = Colored(RedColor)    };
    internal static Tag WarningTag(string text)  => new(text) { Foreground = Colored(OrangeColor) };
    internal static Tag MutedTag(string text)    => new(text);

    private static OptionalColor Colored(Color c) => new() { HasValue = true, Color = c };
}
