using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Commands;

internal sealed partial class SetDefaultModeCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;
    private readonly string _mode;

    public SetDefaultModeCommand(AwakeService awakeService, string mode, string name)
    {
        _awakeService = awakeService;
        _mode = mode;
        Id = $"com.local.nputools.awake.setdefault.{mode}";
        Name = name;
        Icon = AwakeVisuals.Settings;
    }

    public override CommandResult Invoke()
    {
        var settings = _awakeService.GetSettings();
        settings.DefaultAwakeMode = _mode == "screen-off" ? "screen-off" : "indefinite";
        _awakeService.SaveSettings(settings);
        return CommandResult.ShowToast($"Default Awake mode set to {settings.DefaultAwakeMode}.");
    }
}
