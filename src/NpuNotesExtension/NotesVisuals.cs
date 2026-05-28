using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Notes;

internal static class NotesVisuals
{
    internal static readonly IconInfo Notes = new("\uE70B");
    internal static readonly IconInfo Add = new("\uE710");
    internal static readonly IconInfo Search = new("\uE721");
    internal static readonly IconInfo Browse = new("\uE8FD");
    internal static readonly IconInfo Note = new("\uE8D2");
    internal static readonly IconInfo Folder = new("\uE8DA");
    internal static readonly IconInfo Copy = new("\uE8C8");
    internal static readonly IconInfo Pin = new("\uE718");
    internal static readonly IconInfo Delete = new("\uE74D");
    internal static readonly IconInfo Settings = new("\uE713");
    internal static readonly IconInfo Open = new("\uE8A7");
    internal static readonly IconInfo Ai = new("\uED0B");
    internal static readonly IconInfo Related = new("\uE8A0");
    internal static readonly IconInfo Refresh = new("\uE72C");

    private static readonly Color GreenColor = new() { R = 108, G = 203, B = 95, A = 255 };
    private static readonly Color RedColor = new() { R = 255, G = 95, B = 95, A = 255 };

    internal static Tag StatusTag(string text) => new(text) { Foreground = Colored(GreenColor) };
    internal static Tag CriticalTag(string text) => new(text) { Foreground = Colored(RedColor) };
    internal static Tag MutedTag(string text) => new(text);

    private static OptionalColor Colored(Color c) => new() { HasValue = true, Color = c };
}
