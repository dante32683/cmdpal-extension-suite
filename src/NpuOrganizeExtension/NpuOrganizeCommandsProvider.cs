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

        var hub     = new ListItem(new OrganizeHubPage(_scanner, _indexService))                          { Title = "NPU Organize",              Icon = OrganizeVisuals.Folder  };
        var search  = new ListItem(new ScreenshotSearchPage(_indexService))                               { Title = "Search Screenshots",        Icon = OrganizeVisuals.Search  };
        var rename  = new ListItem(new ScreenshotRenameListPage(_scanner, _indexService))                 { Title = "Rename New Screenshots",    Icon = OrganizeVisuals.Rename  };
        var dryRun  = new ListItem(new ScreenshotRenameListPage(_scanner, _indexService, dryRun: true))   { Title = "Dry Run Screenshot Rename", Icon = OrganizeVisuals.DryRun  };
        var indexAll= new ListItem(new IndexAllPage(_scanner, _indexService))                             { Title = "Index All Screenshots",     Icon = OrganizeVisuals.Watcher };
        var watcher = new ListItem(new WatcherDashboardPage())                                            { Title = "Screenshot Watcher",        Icon = OrganizeVisuals.Watcher };

        _commands = [hub, search, rename, dryRun, indexAll, watcher];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
