using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;
using Windows.ApplicationModel.DataTransfer;

namespace NpuTools.Obsidian.Pages;

// Async page: navigating here starts a Phi summarization on first GetItems().
internal sealed partial class SummarizeNotePage : ListPage
{
    private readonly ObsidianNote _note;
    private readonly ObsidianAiService _ai;
    private readonly ObsidianIndexStore _indexStore;
    private int _started;
    private string? _summary;
    private string? _errorMessage;

    public SummarizeNotePage(ObsidianNote note, ObsidianAiService ai, ObsidianIndexStore indexStore)
    {
        _note = note;
        _ai = ai;
        _indexStore = indexStore;
        Id = "com.local.nputools.obsidian.summarize";
        Title = $"Summarize: {note.Title}";
        Name = "Summarize";
        Icon = ObsidianVisuals.Ai;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunAsync);

        if (_errorMessage is not null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Summarize failed",
                    Subtitle = _errorMessage,
                    Icon = ObsidianVisuals.Warning,
                    Tags = [ObsidianVisuals.WarningTag("error")],
                },
            ];
        }

        if (_summary is null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Summarizing with Phi...",
                    Subtitle = _note.Title,
                    Icon = ObsidianVisuals.Ai,
                },
            ];
        }

        return
        [
            new ListItem(new SaveSummaryCommand(_indexStore, _note.AbsolutePath, _summary))
            {
                Title = _summary,
                Subtitle = "Press Enter to save summary to index",
                Icon = ObsidianVisuals.Check,
                Tags = [ObsidianVisuals.StatusTag("Phi summary")],
                MoreCommands =
                [
                    new CommandContextItem(new CopySummaryCommand(_summary)) { Icon = ObsidianVisuals.Copy },
                ],
            },
        ];
    }

    private async Task RunAsync()
    {
        try
        {
            _summary = await _ai.SummarizeAsync(_note.Title, _note.Body);
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

    private sealed partial class SaveSummaryCommand : InvokableCommand
    {
        private readonly ObsidianIndexStore _indexStore;
        private readonly string _absolutePath;
        private readonly string _summary;

        public SaveSummaryCommand(ObsidianIndexStore indexStore, string absolutePath, string summary)
        {
            _indexStore = indexStore;
            _absolutePath = absolutePath;
            _summary = summary;
            Name = "Save to Index";
            Icon = ObsidianVisuals.Check;
        }

        public override CommandResult Invoke()
        {
            _indexStore.UpdateSummary(_absolutePath, _summary);
            return CommandResult.ShowToast("Summary saved to index.");
        }
    }

    private sealed partial class CopySummaryCommand : InvokableCommand
    {
        private readonly string _summary;

        public CopySummaryCommand(string summary)
        {
            _summary = summary;
            Name = "Copy Summary";
            Icon = ObsidianVisuals.Copy;
        }

        public override CommandResult Invoke()
        {
            var package = new DataPackage();
            package.SetText(_summary);
            Clipboard.SetContent(package);
            return CommandResult.ShowToast("Summary copied.");
        }
    }
}
