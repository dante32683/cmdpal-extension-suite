using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Commands;
using NpuTools.Organize.Models;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

internal sealed partial class ScreenshotRenameListPage : ListPage
{
    private readonly ScreenshotScannerService _scanner;
    private readonly ScreenshotIndexService _indexService;
    private readonly bool _dryRun;

    private int _initialized; // 0 = not started, 1 = started, 2 = completed
    private IListItem[] _items = [];

    public ScreenshotRenameListPage(ScreenshotScannerService scanner, ScreenshotIndexService indexService, bool dryRun = false)
    {
        _scanner      = scanner;
        _indexService = indexService;
        _dryRun       = dryRun;
        Id       = dryRun
            ? "com.local.nputools.organize.dryrun"
            : "com.local.nputools.organize.rename";
        Title    = dryRun ? "Preview Screenshot Rename" : "Rename Screenshots";
        Name     = dryRun ? "Preview" : "Rename";
        Icon     = dryRun ? OrganizeVisuals.DryRun : OrganizeVisuals.Rename;
        IsLoading = true; // Indicate loading state initially
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
        {
            // First call to GetItems, start loading asynchronously
            _ = Task.Run(LoadItemsAsync);
        }

        // Return current items (empty or loading indicator) while loading
        if (_initialized < 2)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "Scanning for screenshots...",
                    Subtitle = "Please wait",
                    Icon     = OrganizeVisuals.Check, // Or a loading spinner icon if available
                },
            ];
        }

        return _items;
    }

    private async Task LoadItemsAsync()
    {
        try
        {
            IReadOnlyList<RenameProposal> proposals = await _scanner.ScanAsync();
            var newItems = new List<IListItem>();

            if (proposals.Count == 0)
            {
                newItems.Add(new ListItem(new NoOpCommand())
                {
                    Title    = "No screenshots to rename",
                    Subtitle = _scanner.ScreenshotsFolder,
                    Icon     = OrganizeVisuals.Check,
                });
            }
            else
            {
                if (!_dryRun)
                {
                    newItems.Add(new ListItem(new RenameAllPage(proposals, _indexService))
                    {
                        Title    = $"Rename All ({proposals.Count})",
                        Subtitle = "AI-rename and index every screenshot below",
                        Icon     = OrganizeVisuals.Check,
                        Tags     = [OrganizeVisuals.MutedTag("batch")],
                    });
                }

                newItems.Add(new ListItem(new OpenFolderCommand(_scanner.ScreenshotsFolder))
                {
                    Title = "Open Screenshots Folder",
                    Icon  = OrganizeVisuals.Folder,
                });

                foreach (var proposal in proposals)
                {
                    string date    = System.IO.File.GetCreationTime(proposal.OriginalPath).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                    string preview = _dryRun ? $"→ {date}_[AI title].png" : $"→ {date}_[AI reads on rename].png";
                    var item = new ListItem(_dryRun ? new NoOpCommand() : new RenameSingleCommand(proposal, _indexService))
                    {
                        Title    = proposal.OriginalName,
                        Subtitle = preview,
                        Icon     = OrganizeVisuals.File,
                        Tags     = [OrganizeVisuals.MutedTag(_dryRun ? "preview" : "rename")],
                    };
                    newItems.Add(item);
                }
            }
            _items = newItems.ToArray();
        }
        finally
        {
            Interlocked.Exchange(ref _initialized, 2); // Mark as completed
            IsLoading = false;
            RaiseItemsChanged(_items.Length);
        }
    }
}
