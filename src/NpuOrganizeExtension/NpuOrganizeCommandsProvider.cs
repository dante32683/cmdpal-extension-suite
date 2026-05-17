using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Pages;
using NpuTools.Organize.Services;

namespace NpuTools.Organize;

internal sealed partial class NpuOrganizeCommandsProvider : CommandProvider
{
    private readonly ScreenshotScannerService _scanner = new();
    private readonly ICommandItem[] _commands;

    public NpuOrganizeCommandsProvider()
    {
        Id          = "com.local.nputools.organize";
        DisplayName = "NPU Organize";
        Icon        = OrganizeVisuals.Folder;

        var hub     = new ListItem(new OrganizeHubPage(_scanner))          { Title = "NPU Organize",              Icon = OrganizeVisuals.Folder  };
        var rename  = new ListItem(new ScreenshotRenameListPage(_scanner))  { Title = "Rename New Screenshots",    Icon = OrganizeVisuals.Rename  };
        var dryRun  = new ListItem(new ScreenshotRenameListPage(_scanner, dryRun: true)) { Title = "Dry Run Screenshot Rename", Icon = OrganizeVisuals.DryRun };
        var watcher = new ListItem(new WatcherDashboardPage())              { Title = "Screenshot Watcher",        Icon = OrganizeVisuals.Watcher };

        _commands = [hub, rename, dryRun, watcher];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
