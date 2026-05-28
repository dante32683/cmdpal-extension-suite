using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Models;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

// Page-as-command: the user clicks "Rename All" in the list, which navigates
// here. On first GetItems() the async rename loop starts so no work fires
// until the user has actually confirmed the action by navigating to this page.
internal sealed partial class RenameAllPage : ListPage
{
    private readonly IReadOnlyList<RenameProposal> _proposals;
    private readonly ScreenshotIndexService _indexService;
    private int _success = -1;
    private int _failed  = -1;
    private int _started; // Interlocked flag: 0 = not started, 1 = started

    public RenameAllPage(IReadOnlyList<RenameProposal> proposals, ScreenshotIndexService indexService)
    {
        _proposals    = proposals;
        _indexService = indexService;
        Id    = "com.local.nputools.organize.rename-all";
        Title = $"Process All ({proposals.Count})";
        Name  = "Process All";
        Icon  = OrganizeVisuals.Check;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _ = Task.Run(RunRenameAsync);
        }

        if (_success < 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Processing…",
                    Subtitle = $"Running OCR, AI rename, and indexing {_proposals.Count} screenshot{(_proposals.Count == 1 ? "" : "s")}",
                    Icon     = OrganizeVisuals.Check,
                },
            ];
        }

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title    = $"Processed {_success} screenshot{(_success == 1 ? "" : "s")}",
                Subtitle = _failed > 0 ? $"{_failed} failed — check permissions" : "All screenshots renamed and indexed",
                Icon     = _failed > 0 ? OrganizeVisuals.Warning : OrganizeVisuals.Check,
            },
        ];
    }

    private async Task RunRenameAsync()
    {
        int success = 0;
        int failed  = 0;

        foreach (var p in _proposals)
        {
            try
            {
                var (destination, description, ocrText) = await AiNamingService.BuildProposedPathWithDataAsync(p.OriginalPath);
                File.Move(p.OriginalPath, destination, overwrite: false);
                _indexService.Upsert(destination, description, ocrText);
                success++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Rename failed for '{p.OriginalPath}': {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }

        _success = success;
        _failed  = failed;
        IsLoading = false;
        RaiseItemsChanged();
    }
}
