using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.ImageEditor.Commands;
using NpuTools.ImageEditor.Services;
using static NpuTools.ImageEditor.KeyChords;

namespace NpuTools.ImageEditor.Pages;

// Runs a batch over the picked paths, showing a live "Processing N / Total…" header and, on
// completion, a summary row (open the output folder) plus one row per failed file.
internal sealed partial class BatchResultPage : ListPage
{
    private readonly ImageOperation _operation;
    private readonly int _scaleFactor;
    private readonly IReadOnlyList<string> _paths;
    private readonly ImageEditorSettingsManager _settings;
    private readonly CancellationTokenSource _cts = new();

    private int _started;
    private BatchProgress _progress;
    private BatchSummary? _summary;
    private string? _errorMessage;

    public BatchResultPage(
        ImageOperation operation,
        int scaleFactor,
        IReadOnlyList<string> paths,
        ImageEditorSettingsManager settings)
    {
        _operation   = operation;
        _scaleFactor = scaleFactor;
        _paths       = paths;
        _settings    = settings;

        Id    = $"com.local.nputools.imageeditor.batchresult.{operation.ToString().ToLowerInvariant()}.{scaleFactor}";
        Title = $"Batch: {ImageInputPage.OperationLabel(operation, scaleFactor)}";
        Name  = "Result";
        Icon  = ImageEditorVisuals.RunBatch;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunBatchAsync);

        if (_summary is null && _errorMessage is null)
        {
            int total = _paths.Count;
            int current = _progress.Total == 0 ? 0 : _progress.Current;
            string sub = _progress.Total == 0
                ? "Starting…"
                : $"{_progress.FileName} ({current} / {total})";

            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = $"Processing… {current} / {total}",
                    Subtitle = sub,
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
                    Title    = "Batch failed",
                    Subtitle = _errorMessage,
                    Icon     = ImageEditorVisuals.Error,
                },
            ];
        }

        var summary = _summary!;
        var items = new List<IListItem>
        {
            new ListItem(new OpenFileCommand(summary.OutputDir))
            {
                Title    = $"{summary.Succeeded} succeeded, {summary.Failed} failed",
                Subtitle = summary.OutputDir,
                Icon     = summary.Failed == 0 ? ImageEditorVisuals.Check : ImageEditorVisuals.Error,
                MoreCommands =
                [
                    new CommandContextItem(new RevealInExplorerCommand(summary.OutputDir)) { RequestedShortcut = Reveal },
                    new CommandContextItem(new CopyTextCommand(summary.OutputDir) { Name = "Copy Folder Path", Icon = ImageEditorVisuals.Copy }) { RequestedShortcut = CopyPath },
                ],
            },
        };

        foreach (var r in summary.Results)
        {
            if (r.Success) continue;
            items.Add(new ListItem(new CopyTextCommand(r.Error ?? "Unknown error"))
            {
                Title    = Path.GetFileName(r.Path),
                Subtitle = r.Error ?? "Unknown error",
                Icon     = ImageEditorVisuals.Error,
            });
        }

        return [.. items];
    }

    private async Task RunBatchAsync()
    {
        try
        {
            var progress = new Progress<BatchProgress>(p =>
            {
                _progress = p;
                RaiseItemsChanged();
            });

            _summary = await ImageEditorService.BatchProcessAsync(
                _paths, _operation, _scaleFactor, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _errorMessage = "Batch cancelled.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _cts.Dispose();
            RaiseItemsChanged();
        }
    }
}
