using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.CommandPalette.Extensions;

namespace NpuTools.Clipboard;

internal static class ClipboardVisuals
{
    internal static readonly IconInfo Clipboard = new("\uE8C8");
    internal static readonly IconInfo Copy = new("\uE8C8");
    internal static readonly IconInfo Paste = new("\uE77F");
    internal static readonly IconInfo Text = new("\uE8D2");
    internal static readonly IconInfo Image = new("\uEB9F");
    internal static readonly IconInfo File = new("\uE8A5");
    internal static readonly IconInfo Link = new("\uE71B");
    internal static readonly IconInfo Mail = new("\uE715");
    internal static readonly IconInfo Color = new("\uE790");
    internal static readonly IconInfo Pin = new("\uE718");
    internal static readonly IconInfo Delete = new("\uE74D");
    internal static readonly IconInfo Rename = new("\uE8AC");
    internal static readonly IconInfo Search = new("\uE721");
    internal static readonly IconInfo Settings = new("\uE713");
    internal static readonly IconInfo Warning = new("\uE7BA");
    internal static readonly IconInfo Start = new("\uE768");
    internal static readonly IconInfo Stop = new("\uE71A");

    private static readonly Color GreenColor = new() { R = 108, G = 203, B = 95, A = 255 };
    private static readonly Color YellowColor = new() { R = 255, G = 192, B = 0, A = 255 };
    private static readonly Color RedColor = new() { R = 255, G = 95, B = 95, A = 255 };

    internal static Tag StatusTag(string text) => new(text) { Foreground = Colored(GreenColor) };
    internal static Tag WarningTag(string text) => new(text) { Foreground = Colored(YellowColor) };
    internal static Tag CriticalTag(string text) => new(text) { Foreground = Colored(RedColor) };
    internal static Tag MutedTag(string text) => new(text);

    private static OptionalColor Colored(Color c) => new() { HasValue = true, Color = c };
}
