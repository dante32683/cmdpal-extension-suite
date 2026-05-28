using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Obsidian;

internal static class ObsidianVisuals
{
    internal static readonly IconInfo Hub      = new("\uE70B");
    internal static readonly IconInfo Search   = new("\uE721");
    internal static readonly IconInfo Note     = new("\uE8D2");
    internal static readonly IconInfo Add      = new("\uE710");
    internal static readonly IconInfo Open     = new("\uE8A7");
    internal static readonly IconInfo Edit     = new("\uE70F");
    internal static readonly IconInfo Append   = new("\uE710");
    internal static readonly IconInfo Folder   = new("\uE8DA");
    internal static readonly IconInfo Copy     = new("\uE8C8");
    internal static readonly IconInfo Pin      = new("\uE718");
    internal static readonly IconInfo Delete   = new("\uE74D");
    internal static readonly IconInfo Settings = new("\uE713");
    internal static readonly IconInfo Link     = new("\uE71B");
    internal static readonly IconInfo Daily    = new("\uE787");
    internal static readonly IconInfo Warning  = new("\uE814");
    internal static readonly IconInfo Index    = new("\uE8A1");
    internal static readonly IconInfo Check    = new("\uE73E");
    internal static readonly IconInfo Ai       = new("\uE945"); // Sparkle/AI \u2014 matches TextToolsVisuals.Phi
    internal static readonly IconInfo Related  = new("\uE8A0");

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