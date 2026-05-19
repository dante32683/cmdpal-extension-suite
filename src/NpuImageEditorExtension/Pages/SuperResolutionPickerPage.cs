using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.ImageEditor.Pages;

internal sealed partial class SuperResolutionPickerPage : ListPage
{
    private readonly ImageEditorSettingsManager _settings;

    public SuperResolutionPickerPage(ImageEditorSettingsManager settings)
    {
        _settings = settings;
        Id    = "com.local.nputools.imageeditor.sr.picker";
        Title = "Super Resolution";
        Name  = "Select Scale";
        Icon  = ImageEditorVisuals.Scale;
    }

    public override IListItem[] GetItems() =>
    [
        MakeScaleItem(2,  "Moderate upscale, fastest processing"),
        MakeScaleItem(4,  "High upscale, moderate processing time"),
        MakeScaleItem(8,  "Maximum upscale, slowest processing"),
    ];

    private ListItem MakeScaleItem(int scale, string subtitle) =>
        new(new ImageInputPage(ImageOperation.SuperResolution, scale, _settings))
        {
            Title    = $"Super Resolution ({scale}×)",
            Subtitle = subtitle,
            Icon     = ImageEditorVisuals.Scale,
            Tags     = [ImageEditorVisuals.MutedTag("browse or paste path")],
        };
}
