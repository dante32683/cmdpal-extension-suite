using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.ImageEditor.Pages;

namespace NpuTools.ImageEditor;

internal sealed partial class NpuImageEditorCommandsProvider : CommandProvider
{
    private readonly ImageEditorSettingsManager _settingsManager = new();
    private readonly ICommandItem[] _commands;

    public NpuImageEditorCommandsProvider()
    {
        Id          = "com.local.nputools.imageeditor";
        DisplayName = "NPU Image Editor";
        Icon        = ImageEditorVisuals.Camera;
        Settings    = _settingsManager.Settings;

        var hub = new ListItem(new ImageToolsHubPage(_settingsManager))
        {
            Title    = "Image Editor",
            Subtitle = "AI-powered image processing tools",
            Icon     = ImageEditorVisuals.Camera,
        };

        var rmbg = new ListItem(new ImageInputPage(ImageOperation.RemoveBackground, 1, _settingsManager))
        {
            Title    = "Remove Background",
            Subtitle = "Automatically remove image background using Windows AI",
            Icon     = ImageEditorVisuals.Eraser,
        };

        var sr2 = new ListItem(new ImageInputPage(ImageOperation.SuperResolution, 2, _settingsManager))
        {
            Title    = "Super Resolution (2×)",
            Subtitle = "Upscale an image to 2× resolution using Windows AI",
            Icon     = ImageEditorVisuals.Scale,
        };

        var sr4 = new ListItem(new ImageInputPage(ImageOperation.SuperResolution, 4, _settingsManager))
        {
            Title    = "Super Resolution (4×)",
            Subtitle = "Upscale an image to 4× resolution using Windows AI",
            Icon     = ImageEditorVisuals.Scale,
        };

        var sr8 = new ListItem(new ImageInputPage(ImageOperation.SuperResolution, 8, _settingsManager))
        {
            Title    = "Super Resolution (8×)",
            Subtitle = "Upscale an image to 8× resolution using Windows AI",
            Icon     = ImageEditorVisuals.Scale,
        };

        var ocr = new ListItem(new ImageInputPage(ImageOperation.Ocr, 1, _settingsManager))
        {
            Title    = "OCR: Extract Text",
            Subtitle = "Extract visible text from any image",
            Icon     = ImageEditorVisuals.Ocr,
        };

        _commands = [hub, rmbg, sr2, sr4, sr8, ocr];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
