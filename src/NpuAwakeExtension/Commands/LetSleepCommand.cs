using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Commands;

internal sealed partial class LetSleepCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;

    public LetSleepCommand(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.letsleep";
        Name = "Let Sleep";
        Icon = AwakeVisuals.Moon;
    }

    public override CommandResult Invoke()
    {
        _awakeService.SetOverride(null);
        return CommandResult.ShowToast("PC can now sleep.");
    }
}
