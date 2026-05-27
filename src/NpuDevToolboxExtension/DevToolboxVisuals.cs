using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.DevToolbox;

internal static class DevToolboxVisuals
{
    internal static readonly IconInfo Toolbox    = new("\uE943");
    internal static readonly IconInfo Workspace  = new("\uE8DA");
    internal static readonly IconInfo Explorer   = new("\uED25");
    internal static readonly IconInfo Terminal   = new("\uE756");
    internal static readonly IconInfo Ide        = new("\uE8F4");
    internal static readonly IconInfo Settings   = new("\uE713");
    internal static readonly IconInfo Copy       = new("\uE8C8");
    internal static readonly IconInfo Remove     = new("\uE74D");
    internal static readonly IconInfo Recent     = new("\uE81C");
    internal static readonly IconInfo Scan       = new("\uE721");

    private static readonly Color GreenColor = new() { R = 108, G = 203, B = 95, A = 255 };
    private static readonly Color MutedColor = new() { R = 150, G = 150, B = 150, A = 255 };

    internal static Tag StatusTag(string text) => new(text) { Foreground = Colored(GreenColor) };
    internal static Tag MutedTag(string text)  => new(text) { Foreground = Colored(MutedColor) };

    private static OptionalColor Colored(Color c) => new() { HasValue = true, Color = c };
}