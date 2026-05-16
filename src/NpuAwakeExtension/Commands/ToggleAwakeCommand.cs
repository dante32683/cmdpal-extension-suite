using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Commands;

internal sealed partial class ToggleAwakeCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;

    public ToggleAwakeCommand(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.toggle";
        Name = "Awake";
        Icon = AwakeVisuals.Power;
    }

    public override CommandResult Invoke()
    {
        bool ok = _awakeService.ToggleDefaultAwake();
        return CommandResult.ShowToast(ok ? "Awake state updated." : "Awake keeper could not be started.");
    }
}
