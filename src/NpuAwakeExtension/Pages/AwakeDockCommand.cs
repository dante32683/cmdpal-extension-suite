using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Pages;

internal sealed partial class AwakeDockCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;

    public AwakeDockCommand(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.dock";
        Name = "Awake";
        Icon = AwakeVisuals.Power;
    }

    public override CommandResult Invoke()
    {
        bool ok = _awakeService.ToggleDefaultAwake();
        return ok ? CommandResult.KeepOpen() : CommandResult.ShowToast("Awake keeper could not be started.");
    }
}
