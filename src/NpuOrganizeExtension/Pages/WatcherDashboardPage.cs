using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Organize.Commands;

namespace NpuTools.Organize.Pages;

internal sealed partial class WatcherDashboardPage : ListPage
{
    private static readonly string KeeperPath = Path.Combine(
        AppContext.BaseDirectory, "NpuOrganizeKeeper.exe");

    public WatcherDashboardPage()
    {
        Id    = "com.local.nputools.organize.watcher";
        Title = "Screenshot Watcher";
        Name  = "Watcher";
        Icon  = OrganizeVisuals.Watcher;
    }

    public override IListItem[] GetItems()
    {
        bool keeperInstalled = File.Exists(KeeperPath);
        int? keeperPid       = FindKeeperPid();
        bool running         = keeperPid.HasValue;

        if (!keeperInstalled)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title    = "OrganizeKeeper not installed",
                    Subtitle = "The watcher daemon (NpuOrganizeKeeper.exe) is not present alongside this extension.",
                    Icon     = OrganizeVisuals.Warning,
                    Tags     = [OrganizeVisuals.MutedTag("not available")],
                },
            ];
        }

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title    = running ? $"Running — PID {keeperPid}" : "Stopped",
                Subtitle = running ? "OrganizeKeeper is active and watching for screenshots." : "OrganizeKeeper is not running.",
                Icon     = running ? OrganizeVisuals.Start : OrganizeVisuals.Stop,
                Tags     = [OrganizeVisuals.MutedTag(running ? "running" : "stopped")],
            },
            new ListItem(new StartStopKeeperCommand(KeeperPath, running))
            {
                Title = running ? "Stop Watcher" : "Start Watcher",
                Icon  = running ? OrganizeVisuals.Stop : OrganizeVisuals.Start,
            },
        ];
    }

    private static int? FindKeeperPid()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("NpuOrganizeKeeper"))
            {
                return proc.Id;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Organize watcher pid check failed: {ex}");
        }

        return null;
    }
}
