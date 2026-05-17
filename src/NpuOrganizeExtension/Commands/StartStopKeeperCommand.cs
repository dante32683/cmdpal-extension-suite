using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Organize.Commands;

internal sealed partial class StartStopKeeperCommand : InvokableCommand
{
    private static readonly string StopFlagPath = Path.Combine(
        Environment.GetEnvironmentVariable("LOCALAPPDATA")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NpuOrganize", "stop.flag");

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
                // Write stop.flag — the keeper's main loop detects it and exits cleanly.
                Directory.CreateDirectory(Path.GetDirectoryName(StopFlagPath)!);
                File.WriteAllText(StopFlagPath, string.Empty);
            }
            else
            {
                Process.Start(new ProcessStartInfo(_keeperPath)
                {
                    UseShellExecute  = true,
                    WindowStyle      = ProcessWindowStyle.Hidden,
                    CreateNoWindow   = true,
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Organize keeper toggle failed: {ex}");
        }

        return CommandResult.Dismiss();
    }
}
