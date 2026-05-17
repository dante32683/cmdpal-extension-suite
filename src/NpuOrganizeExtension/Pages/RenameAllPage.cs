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
    private int _success = -1;
    private int _failed  = -1;
    private int _started; // Interlocked flag: 0 = not started, 1 = started

    public RenameAllPage(IReadOnlyList<RenameProposal> proposals)
    {
        _proposals = proposals;
        Id    = "com.local.nputools.organize.rename-all";
        Title = $"Rename All ({proposals.Count})";
        Name  = "Rename All";
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
                    Title    = "Renaming…",
                    Subtitle = $"{_proposals.Count} file{(_proposals.Count == 1 ? "" : "s")}",
                    Icon     = OrganizeVisuals.Check,
                },
            ];
        }

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title    = $"Renamed {_success} file{(_success == 1 ? "" : "s")}",
                Subtitle = _failed > 0 ? $"{_failed} failed — check permissions" : "All files renamed successfully",
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
                string destination = await AiNamingService.BuildProposedPathAsync(p.OriginalPath);
                File.Move(p.OriginalPath, destination, overwrite: false);
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
