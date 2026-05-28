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
                Title    = "Rename Screenshots",
                Subtitle = "AI-rename and index screenshots that haven't been organized",
                Icon     = OrganizeVisuals.Rename,
                Tags     = [OrganizeVisuals.MutedTag("rename")],
            },
            new ListItem(new ScreenshotRenameListPage(_scanner, _indexService, dryRun: true))
            {
                Title    = "Preview Screenshot Rename",
                Subtitle = "Preview AI rename proposals without making any changes",
                Icon     = OrganizeVisuals.DryRun,
                Tags     = [OrganizeVisuals.MutedTag("preview")],
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
