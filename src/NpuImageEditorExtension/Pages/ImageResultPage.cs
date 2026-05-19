using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.ImageEditor.Services;

namespace NpuTools.ImageEditor.Pages;

internal sealed partial class ImageResultPage : ListPage
{
    private readonly ImageOperation _operation;
    private readonly int _scaleFactor;
    private readonly string _path;
    private readonly ImageEditorSettingsManager _settings;
    private readonly bool _isText;

    private string? _result;
    private string? _errorMessage;
    private int _started;

    public ImageResultPage(
        ImageOperation operation,
        int scaleFactor,
        string path,
        ImageEditorSettingsManager settings)
    {
        _operation   = operation;
        _scaleFactor = scaleFactor;
        _path        = path;
        _settings    = settings;
        _isText      = operation == ImageOperation.Ocr;

        Id    = $"com.local.nputools.imageeditor.result.{operation.ToString().ToLowerInvariant()}.{scaleFactor}";
        Title = $"Result — {ImageInputPage.OperationLabel(operation, scaleFactor)}";
        Name  = "Result";
        Icon  = ImageEditorVisuals.Check;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunOperationAsync);

        if (_result == null && _errorMessage == null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Processing…",
                    Subtitle = ImageInputPage.OperationLabel(_operation, _scaleFactor),
                    Icon     = ImageInputPage.OperationIcon(_operation),
                },
            ];
        }

        if (_errorMessage is not null)
        {
            return
            [
                new ListItem(new CopyTextCommand(_errorMessage))
                {
                    Title    = "Operation failed — press Enter to copy error",
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
                Subtitle = Path.GetFileName(result),
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
                ImageOperation.SuperResolution  => await ImageEditorService.SuperResolutionAsync(_path, _scaleFactor),
                ImageOperation.Ocr              => await ImageEditorService.RunOcrAsync(_path),
                _                               => throw new ArgumentOutOfRangeException(nameof(_operation)),
            };

            ApplyAutoActions(_result);
        }
        catch (Exception ex)
        {
            _errorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }

    private void ApplyAutoActions(string result)
    {
        var s = _settings.Current;

        if (_isText)
        {
            if (s.OcrAutoCopyText)
                ClipboardHelper.SetText(result);

            if (s.OcrAutoOpenTextFile)
            {
                string txtPath = Path.Combine(
                    ImageEditorPaths.SupportDir(),
                    $"ocr_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(txtPath, result);
                Process.Start(new ProcessStartInfo(txtPath) { UseShellExecute = true });
            }
        }
        else if (s.AutoOpenResult)
        {
            Process.Start(new ProcessStartInfo(result) { UseShellExecute = true });
        }
    }
}
