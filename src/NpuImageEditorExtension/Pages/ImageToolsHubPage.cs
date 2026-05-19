using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.ImageEditor.Pages;

internal sealed partial class ImageToolsHubPage : ListPage
{
    private readonly ImageEditorSettingsManager _settings;

    public ImageToolsHubPage(ImageEditorSettingsManager settings)
    {
        _settings = settings;
        Id    = "com.local.nputools.imageeditor.hub";
        Title = "Image Editor";
        Name  = "Open";
        Icon  = ImageEditorVisuals.Camera;
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new ImageInputPage(ImageOperation.RemoveBackground, 1, _settings))
        {
            Title    = ImageInputPage.OperationLabel(ImageOperation.RemoveBackground, 1),
            Subtitle = "Isolate subject automatically using Windows AI",
            Icon     = ImageInputPage.OperationIcon(ImageOperation.RemoveBackground),
            Tags     = [ImageEditorVisuals.MutedTag("browse or paste path")],
        },
        new ListItem(new SuperResolutionPickerPage(_settings))
        {
            Title    = "Super Resolution",
            Subtitle = "Upscale an image 2×, 4×, or 8× using Windows AI",
            Icon     = ImageEditorVisuals.Scale,
        },
        new ListItem(new ImageInputPage(ImageOperation.Ocr, 1, _settings))
        {
            Title    = ImageInputPage.OperationLabel(ImageOperation.Ocr, 1),
            Subtitle = "Extract visible text from any image",
            Icon     = ImageInputPage.OperationIcon(ImageOperation.Ocr),
            Tags     = [ImageEditorVisuals.MutedTag("browse or paste path")],
        },
    ];
}
