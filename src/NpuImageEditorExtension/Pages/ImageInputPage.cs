using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.ImageEditor.Pages;

internal enum ImageOperation { RemoveBackground, SuperResolution, Ocr }

internal sealed partial class ImageInputPage : DynamicListPage
{
    private readonly ImageOperation _operation;
    private IListItem[] _items;

    public ImageInputPage(ImageOperation operation)
    {
        _operation      = operation;
        Id              = $"com.local.nputools.imageeditor.{operation.ToString().ToLowerInvariant()}";
        Title           = OperationLabel(operation);
        Name            = "Run";
        Icon            = OperationIcon(operation);
        PlaceholderText = "Paste full image path here…";
        _items          = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim().Trim('"'));
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string path)
    {
        if (path.Length == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = OperationLabel(_operation),
                    Subtitle = "Paste the full path to an image file above, then press Enter",
                    Icon     = OperationIcon(_operation),
                },
            ];
        }

        if (!File.Exists(path))
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "File not found",
                    Subtitle = path,
                    Icon     = ImageEditorVisuals.Folder,
                },
            ];
        }

        return
        [
            new ListItem(new ImageResultPage(path, _operation))
            {
                Title    = $"{OperationLabel(_operation)} — {Path.GetFileName(path)}",
                Subtitle = path,
                Icon     = OperationIcon(_operation),
                Tags     = [ImageEditorVisuals.MutedTag("press Enter")],
            },
        ];
    }

    internal static string OperationLabel(ImageOperation op) => op switch
    {
        ImageOperation.RemoveBackground => "Remove Background",
        ImageOperation.SuperResolution  => "Super Resolution (2x)",
        ImageOperation.Ocr              => "Extract Text (OCR)",
        _                               => op.ToString(),
    };

    internal static IconInfo OperationIcon(ImageOperation op) => op switch
    {
        ImageOperation.RemoveBackground => ImageEditorVisuals.Eraser,
        ImageOperation.SuperResolution  => ImageEditorVisuals.Scale,
        ImageOperation.Ocr              => ImageEditorVisuals.Ocr,
        _                               => ImageEditorVisuals.Camera,
    };
}
