using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Pages;
using NpuTools.Organize.Services;

namespace NpuTools.Organize;

internal sealed partial class NpuOrganizeCommandsProvider : CommandProvider
{
    private readonly ScreenshotScannerService _scanner      = new();
    private readonly ScreenshotIndexService   _indexService = new();
    private readonly ICommandItem[] _commands;

    public NpuOrganizeCommandsProvider()
    {
        Id          = "com.local.nputools.organize";
        DisplayName = "NPU Organize";
        Icon        = OrganizeVisuals.Folder;

        var hub     = new ListItem(new OrganizeHubPage(_scanner, _indexService))                        { Title = "Organize",                          Subtitle = "Screenshot processing and search hub",                        Icon = OrganizeVisuals.Folder  };
        var search  = new ListItem(new ScreenshotSearchPage(_indexService, _scanner))                   { Title = "Search Screenshots",                Subtitle = "Search by content or AI description",                         Icon = OrganizeVisuals.Search  };
        var index   = new ListItem(new IndexAllPage(_scanner, _indexService))                           { Title = "Index Screenshots",                 Subtitle = "Scan screenshots folder and add new items to search index",   Icon = OrganizeVisuals.Search  };
        var process = new ListItem(new ScreenshotRenameListPage(_scanner, _indexService))               { Title = "Rename Screenshots",                Subtitle = "AI-rename and index screenshots that haven't been organized",  Icon = OrganizeVisuals.Rename  };
        var preview = new ListItem(new ScreenshotRenameListPage(_scanner, _indexService, dryRun: true)) { Title = "Preview Screenshot Rename",          Subtitle = "Preview AI rename proposals without making any changes",       Icon = OrganizeVisuals.DryRun  };
        var watcher = new ListItem(new WatcherDashboardPage())                                          { Title = "Screenshot Watcher",                Subtitle = "View and control the background rename daemon",               Icon = OrganizeVisuals.Watcher };

        _commands = [hub, search, index, process, preview, watcher];

        WatcherDashboardPage.EnsureKeeperRunning();
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
