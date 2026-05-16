using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Commands;

internal sealed partial class StopDaemonCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;

    public StopDaemonCommand(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.stopdaemon";
        Name = "Stop Awake Daemon";
        Icon = AwakeVisuals.Stop;
    }

    public override CommandResult Invoke()
    {
        _awakeService.StopDaemon();
        return CommandResult.ShowToast("Awake daemon stopped.");
    }
}
