using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

internal sealed partial class OrganizeHubPage : ListPage
{
    private readonly ScreenshotScannerService _scanner;

    public OrganizeHubPage(ScreenshotScannerService scanner)
    {
        _scanner = scanner;
        Id       = "com.local.nputools.organize.hub";
        Title    = "NPU Organize";
        Name     = "Open";
        Icon     = OrganizeVisuals.Folder;
    }

    public override IListItem[] GetItems()
    {
        return
        [
            new ListItem(new ScreenshotRenameListPage(_scanner))
            {
                Title    = "Rename New Screenshots",
                Subtitle = "Propose date-based renames for unorganized screenshots",
                Icon     = OrganizeVisuals.Rename,
                Tags     = [OrganizeVisuals.MutedTag("batch rename")],
            },
            new ListItem(new ScreenshotRenameListPage(_scanner, dryRun: true))
            {
                Title    = "Dry Run Screenshot Rename",
                Subtitle = "Preview proposals without making any changes",
                Icon     = OrganizeVisuals.DryRun,
                Tags     = [OrganizeVisuals.MutedTag("preview only")],
            },
            new ListItem(new WatcherDashboardPage())
            {
                Title    = "Screenshot Watcher",
                Subtitle = "OrganizeKeeper status and controls",
                Icon     = OrganizeVisuals.Watcher,
            },
        ];
    }
}
