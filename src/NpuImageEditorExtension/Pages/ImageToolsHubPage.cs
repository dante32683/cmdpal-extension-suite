using System.Collections.Generic;
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

    public override IListItem[] GetItems()
    {
        var ops = new List<(ImageOperation Op, int Scale, string Subtitle)>
        {
            (ImageOperation.RemoveBackground, 1,  "Isolate subject automatically using Windows AI"),
            (ImageOperation.SuperResolution,  2,  "Upscale 2× using Windows AI"),
            (ImageOperation.SuperResolution,  4,  "Upscale 4× using Windows AI"),
            (ImageOperation.SuperResolution,  8,  "Upscale 8× using Windows AI"),
            (ImageOperation.Ocr,              1,  "Extract visible text from any image"),
        };

        var items = new List<IListItem>(ops.Count);
        foreach (var (op, scale, subtitle) in ops)
        {
            items.Add(new ListItem(new ImageInputPage(op, scale, _settings))
            {
                Title    = ImageInputPage.OperationLabel(op, scale),
                Subtitle = subtitle,
                Icon     = ImageInputPage.OperationIcon(op),
                Tags     = [ImageEditorVisuals.MutedTag("browse or paste path")],
            });
        }

        return items.ToArray();
    }
}
