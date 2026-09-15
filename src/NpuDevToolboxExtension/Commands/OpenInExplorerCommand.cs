using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox.Commands;

internal sealed partial class OpenInExplorerCommand : InvokableCommand
{
    private readonly string _path;
    private readonly RecentWorkspacesStore _recents;

    public OpenInExplorerCommand(string path, RecentWorkspacesStore recents)
    {
        _path = path;
        _recents = recents;
        Name = "Open in Explorer";
        Icon = DevToolboxVisuals.Explorer;
    }

    public override CommandResult Invoke()
    {
        _recents.Add(_path);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { _path },
                UseShellExecute = false,
            });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Explorer launch failed: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.ShowToast("Could not open this workspace in Explorer.");
        }
    }
}
