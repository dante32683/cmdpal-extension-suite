using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Commands;
using NpuTools.Organize.Models;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

internal sealed partial class ScreenshotRenameListPage : ListPage
{
    private readonly ScreenshotScannerService _scanner;
    private readonly bool _dryRun;

    public ScreenshotRenameListPage(ScreenshotScannerService scanner, bool dryRun = false)
    {
        _scanner = scanner;
        _dryRun  = dryRun;
        Id       = dryRun
            ? "com.local.nputools.organize.dryrun"
            : "com.local.nputools.organize.rename";
        Title    = dryRun ? "Dry Run — Screenshot Rename" : "Rename New Screenshots";
        Name     = dryRun ? "Dry Run" : "Rename";
        Icon     = dryRun ? OrganizeVisuals.DryRun : OrganizeVisuals.Rename;
    }

    public override IListItem[] GetItems()
    {
        IReadOnlyList<RenameProposal> proposals = _scanner.Scan();

        if (proposals.Count == 0)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "No unorganized screenshots found",
                    Subtitle = _scanner.ScreenshotsFolder,
                    Icon     = OrganizeVisuals.Check,
                },
            ];
        }

        var items = new List<IListItem>(proposals.Count + 2);

        if (!_dryRun)
        {
            items.Add(new ListItem(new RenameAllPage(proposals))
            {
                Title    = $"Rename All ({proposals.Count})",
                Subtitle = "Rename every proposal below",
                Icon     = OrganizeVisuals.Check,
                Tags     = [OrganizeVisuals.MutedTag("batch")],
            });
        }

        items.Add(new ListItem(new OpenFolderCommand(_scanner.ScreenshotsFolder))
        {
            Title = "Open Screenshots Folder",
            Icon  = OrganizeVisuals.Folder,
        });

        foreach (var proposal in proposals)
        {
            string date    = System.IO.File.GetCreationTime(proposal.OriginalPath).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string preview = _dryRun ? $"→ {date}_[OCR title].png" : $"→ {date}_[NPU reads on rename].png";
            var item = new ListItem(_dryRun ? new NoOpCommand() : new RenameSingleCommand(proposal))
            {
                Title    = proposal.OriginalName,
                Subtitle = preview,
                Icon     = OrganizeVisuals.File,
                Tags     = [OrganizeVisuals.MutedTag(_dryRun ? "preview" : "rename")],
            };
            items.Add(item);
        }

        return items.ToArray();
    }
}
