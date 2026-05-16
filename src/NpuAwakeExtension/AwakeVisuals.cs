using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Awake;

internal static class AwakeVisuals
{
    public static IconInfo Power { get; } = new("\uE7E8");

    public static IconInfo Clock { get; } = new("\uE916");

    public static IconInfo Calendar { get; } = new("\uE787");

    public static IconInfo Moon { get; } = new("\uE708");

    public static IconInfo Sparkle { get; } = new("\uE945");

    public static IconInfo List { get; } = new("\uE8FD");

    public static IconInfo Stop { get; } = new("\uE71A");

    public static IconInfo Settings { get; } = new("\uE713");

    public static IconInfo Check { get; } = new("\uE73E");

    public static Tag StatusTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(255, 255, 255, 255)),
        Background = new OptionalColor(true, new Color(36, 120, 84, 255)),
    };

    public static Tag MutedTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(210, 210, 210, 255)),
        Background = new OptionalColor(true, new Color(74, 74, 74, 255)),
    };

    public static Tag WarningTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(255, 255, 255, 255)),
        Background = new OptionalColor(true, new Color(164, 100, 28, 255)),
    };
}
