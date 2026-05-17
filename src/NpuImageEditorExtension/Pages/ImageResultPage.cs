using System;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.ImageEditor.Services;

namespace NpuTools.ImageEditor.Pages;

internal sealed partial class ImageResultPage : ListPage
{
    private readonly string _path;
    private readonly ImageOperation _operation;
    private readonly bool _isText;

    private string? _result;
    private string? _errorMessage;

    public ImageResultPage(string path, ImageOperation operation)
    {
        _path      = path;
        _operation = operation;
        _isText    = operation == ImageOperation.Ocr;

        Id    = $"com.local.nputools.imageeditor.result.{operation.ToString().ToLowerInvariant()}";
        Title = $"Result — {ImageInputPage.OperationLabel(operation)}";
        Name  = "Result";
        Icon  = ImageEditorVisuals.Check;
    }

    public override IListItem[] GetItems()
    {
        if (_result == null && _errorMessage == null)
        {
            try
            {
                _result = _operation switch
                {
                    ImageOperation.RemoveBackground => ImageEditorService.RemoveBackgroundAsync(_path).GetAwaiter().GetResult(),
                    ImageOperation.SuperResolution  => ImageEditorService.SuperResolutionAsync(_path).GetAwaiter().GetResult(),
                    ImageOperation.Ocr              => ImageEditorService.RunOcrAsync(_path).GetAwaiter().GetResult(),
                    _                               => throw new ArgumentOutOfRangeException(nameof(_operation)),
                };
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
            }
        }

        if (_errorMessage is not null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Operation failed",
                    Subtitle = _errorMessage,
                    Icon     = ImageEditorVisuals.Folder,
                },
            ];
        }

        string result = _result!;

        if (_isText)
        {
            string preview = result.Length > 200 ? result[..200] + "…" : result;
            return
            [
                new ListItem(new CopyTextCommand(result))
                {
                    Title    = "Copy Extracted Text",
                    Subtitle = preview,
                    Icon     = ImageEditorVisuals.Copy,
                    Tags     = [ImageEditorVisuals.MutedTag("copies to clipboard")],
                },
                new ListItem(new NoOpCommand())
                {
                    Title    = "Extracted Text",
                    Subtitle = result,
                },
            ];
        }

        string fileName = Path.GetFileName(result);
        return
        [
            new ListItem(new OpenFileCommand(result))
            {
                Title    = "Open Output File",
                Subtitle = result,
                Icon     = ImageEditorVisuals.Folder,
            },
            new ListItem(new CopyTextCommand(result))
            {
                Title    = "Copy Output Path",
                Subtitle = fileName,
                Icon     = ImageEditorVisuals.Copy,
            },
        ];
    }
}
