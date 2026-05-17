using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.ImageEditor;

internal static class ImageEditorVisuals
{
    internal static readonly IconInfo Camera  = new("\uE8BA"); // Photo
    internal static readonly IconInfo Crop    = new("\uE7A8"); // Crop
    internal static readonly IconInfo Eraser  = new("\uED60"); // Eraser
    internal static readonly IconInfo Scale   = new("\uE9A0"); // ZoomIn
    internal static readonly IconInfo Ocr     = new("\uE8F4"); // ScanQR
    internal static readonly IconInfo Copy    = new("\uE8C8"); // Copy
    internal static readonly IconInfo Folder  = new("\uE8DA"); // FolderOpen
    internal static readonly IconInfo Check   = new("\uE73E"); // Accept

    internal static Tag MutedTag(string text) => new(text)
    {
        Foreground = new OptionalColor(true, new Color(210, 210, 210, 255)),
        Background = new OptionalColor(true, new Color(74, 74, 74, 255)),
    };
}
