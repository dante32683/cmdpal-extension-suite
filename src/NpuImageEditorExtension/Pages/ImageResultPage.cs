using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    private int _started; // Interlocked flag: 0 = not started, 1 = started

    public ImageResultPage(string path, ImageOperation operation)
    {
        _path      = path;
        _operation = operation;
        _isText    = operation == ImageOperation.Ocr;

        Id    = $"com.local.nputools.imageeditor.result.{operation.ToString().ToLowerInvariant()}";
        Title = $"Result — {ImageInputPage.OperationLabel(operation)}";
        Name  = "Result";
        Icon  = ImageEditorVisuals.Check;
        IsLoading = true;
    }

    // GetItems() is only called after the user navigates to this page. Starting
    // the operation here prevents AI tasks from firing while the user is still
    // typing a path in the input page.
    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _ = Task.Run(RunOperationAsync);
        }

        if (_result == null && _errorMessage == null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Processing…",
                    Subtitle = ImageInputPage.OperationLabel(_operation),
                    Icon     = ImageInputPage.OperationIcon(_operation),
                },
            ];
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

    private async Task RunOperationAsync()
    {
        try
        {
            _result = _operation switch
            {
                ImageOperation.RemoveBackground => await ImageEditorService.RemoveBackgroundAsync(_path),
                ImageOperation.SuperResolution  => await ImageEditorService.SuperResolutionAsync(_path),
                ImageOperation.Ocr              => await ImageEditorService.RunOcrAsync(_path),
                _                               => throw new ArgumentOutOfRangeException(nameof(_operation)),
            };
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }
}
