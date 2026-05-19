using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Commands;
using NpuTools.Awake.Pages;
using NpuTools.Awake.Services;

namespace NpuTools.Awake;

internal sealed partial class NpuAwakeCommandsProvider : CommandProvider
{
    private readonly AwakeService _awakeService;
    private readonly AwakeSettingsManager _settingsManager;
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _dockBands;

    public NpuAwakeCommandsProvider(AwakeService awakeService)
    {
        _awakeService = awakeService;
        _settingsManager = new AwakeSettingsManager(_awakeService);
        Id = "com.local.nputools.awake";
        DisplayName = "NPU Awake";
        Icon = AwakeVisuals.Power;
        Settings = _settingsManager.Settings;

        // Daemon is killed when the extension process is killed (e.g. on CmdPal reload).
        // Restart it on startup if an active override or schedules are still persisted.
        var status = _awakeService.GetStatus();
        if (status.Override != null || status.Schedules.Count > 0)
        {
            _awakeService.EnsureDaemonRunning();
        }

        _commands =
        [
            new CommandItem(new ToggleAwakeCommand(_awakeService)) { Title = "Awake", Subtitle = "Toggle keep-awake using the default mode", Icon = AwakeVisuals.Power },
            new CommandItem(new AwakeForPage(_awakeService)) { Title = "Awake For...", Subtitle = "Type minutes, then press Enter", Icon = AwakeVisuals.Clock },
            new CommandItem(new AwakeUntilPage(_awakeService)) { Title = "Awake Until…", Subtitle = "Type a local time, then press Enter", Icon = AwakeVisuals.Calendar },
            new CommandItem(new LetSleepCommand(_awakeService)) { Title = "Let Sleep", Subtitle = "Cancel active manual keep-awake", Icon = AwakeVisuals.Moon },
            new CommandItem(new AwakeDashboardPage(_awakeService)) { Title = "Awake Dashboard", Subtitle = "Status, shortcuts, daemon controls", Icon = AwakeVisuals.List },
            new CommandItem(new AwakeSchedulesPage(_awakeService)) { Title = "Awake Schedules", Subtitle = "Add, pause, resume, and delete schedules", Icon = AwakeVisuals.Calendar },
            new CommandItem(new SmartAwakePage(_awakeService)) { Title = "Smart Awake", Subtitle = "Type a natural language request, then press Enter", Icon = AwakeVisuals.Sparkle },
        ];

        _dockBands =
        [
            new CommandItem(new AwakeDockCommand(_awakeService)) { Title = "Awake", Icon = Icon },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() => [new AwakeFallbackCommandItem(_awakeService)];

    public override ICommandItem[]? GetDockBands() => _dockBands;
}
