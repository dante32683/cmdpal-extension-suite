using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.ImageEditor.Pages;

internal sealed partial class ImageToolsHubPage : ListPage
{
    public ImageToolsHubPage()
    {
        Id    = "com.local.nputools.imageeditor.hub";
        Title = "Image Editor";
        Name  = "Open";
        Icon  = ImageEditorVisuals.Camera;
    }

    public override IListItem[] GetItems()
    {
        var ops = new List<(ImageOperation Op, string Subtitle)>
        {
            (ImageOperation.RemoveBackground, "Isolate subject using Windows AI"),
            (ImageOperation.SuperResolution,  "Upscale 2x using Windows AI"),
            (ImageOperation.Ocr,              "Extract text from any image"),
        };

        var items = new List<IListItem>(ops.Count);
        foreach (var (op, subtitle) in ops)
        {
            items.Add(new ListItem(new ImageInputPage(op))
            {
                Title    = ImageInputPage.OperationLabel(op),
                Subtitle = subtitle,
                Icon     = ImageInputPage.OperationIcon(op),
                Tags     = [ImageEditorVisuals.MutedTag("paste path")],
            });
        }

        return items.ToArray();
    }
}
