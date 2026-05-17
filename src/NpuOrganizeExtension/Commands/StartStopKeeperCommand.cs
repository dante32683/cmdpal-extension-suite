using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Organize.Commands;

internal sealed partial class StartStopKeeperCommand : InvokableCommand
{
    private readonly string _keeperPath;
    private readonly bool _isRunning;

    public StartStopKeeperCommand(string keeperPath, bool isRunning)
    {
        _keeperPath = keeperPath;
        _isRunning  = isRunning;
        Name        = isRunning ? "Stop Watcher" : "Start Watcher";
        Icon        = isRunning ? OrganizeVisuals.Stop : OrganizeVisuals.Start;
    }

    public override CommandResult Invoke()
    {
        try
        {
            if (_isRunning)
            {
                foreach (var proc in Process.GetProcessesByName("NpuOrganizeKeeper"))
                    proc.Kill();
            }
            else
            {
                Process.Start(new ProcessStartInfo(_keeperPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Organize keeper toggle failed: {ex}");
        }

        return CommandResult.Dismiss();
    }
}
