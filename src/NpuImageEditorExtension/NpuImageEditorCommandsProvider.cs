using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.ImageEditor.Pages;

namespace NpuTools.ImageEditor;

internal sealed partial class NpuImageEditorCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public NpuImageEditorCommandsProvider()
    {
        Id          = "com.local.nputools.imageeditor";
        DisplayName = "NPU Image Editor";
        Icon        = ImageEditorVisuals.Camera;

        var hub     = new ListItem(new ImageToolsHubPage())                              { Title = "Image Editor",      Subtitle = "AI-powered image processing tools",       Icon = ImageEditorVisuals.Camera  };
        var rmbg    = new ListItem(new ImageInputPage(ImageOperation.RemoveBackground)) { Title = "Remove Background", Subtitle = "Remove image background using AI",          Icon = ImageEditorVisuals.Eraser  };
        var upscale = new ListItem(new ImageInputPage(ImageOperation.SuperResolution))  { Title = "Super Resolution",  Subtitle = "Upscale an image to 2x resolution",         Icon = ImageEditorVisuals.Scale   };
        var ocr     = new ListItem(new ImageInputPage(ImageOperation.Ocr))              { Title = "OCR: Extract Text", Subtitle = "Extract visible text from an image",        Icon = ImageEditorVisuals.Ocr     };

        _commands = [hub, rmbg, upscale, ocr];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
