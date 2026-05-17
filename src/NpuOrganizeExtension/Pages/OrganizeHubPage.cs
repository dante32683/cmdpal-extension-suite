using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Services;

namespace NpuTools.Organize.Pages;

internal sealed partial class OrganizeHubPage : ListPage
{
    private readonly ScreenshotScannerService _scanner;
    private readonly ScreenshotIndexService _indexService;

    public OrganizeHubPage(ScreenshotScannerService scanner, ScreenshotIndexService indexService)
    {
        _scanner      = scanner;
        _indexService = indexService;
        Id    = "com.local.nputools.organize.hub";
        Title = "NPU Organize";
        Name  = "Open";
        Icon  = OrganizeVisuals.Folder;
    }

    public override IListItem[] GetItems()
    {
        return
        [
            new ListItem(new ScreenshotSearchPage(_indexService))
            {
                Title    = "Search Screenshots",
                Subtitle = "Find screenshots by OCR text or AI description",
                Icon     = OrganizeVisuals.Search,
                Tags     = [OrganizeVisuals.MutedTag("search")],
            },
            new ListItem(new ScreenshotRenameListPage(_scanner, _indexService))
            {
                Title    = "Rename New Screenshots",
                Subtitle = "Propose date-based renames for unorganized screenshots",
                Icon     = OrganizeVisuals.Rename,
                Tags     = [OrganizeVisuals.MutedTag("batch rename")],
            },
            new ListItem(new ScreenshotRenameListPage(_scanner, _indexService, dryRun: true))
            {
                Title    = "Dry Run Screenshot Rename",
                Subtitle = "Preview proposals without making any changes",
                Icon     = OrganizeVisuals.DryRun,
                Tags     = [OrganizeVisuals.MutedTag("preview only")],
            },
            new ListItem(new IndexAllPage(_scanner, _indexService))
            {
                Title    = "Index All Screenshots",
                Subtitle = "Run OCR and AI description on all screenshots for search",
                Icon     = OrganizeVisuals.Watcher,
                Tags     = [OrganizeVisuals.MutedTag("index")],
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
