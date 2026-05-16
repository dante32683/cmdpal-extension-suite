using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Commands;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Pages;

internal sealed partial class AwakeDashboardPage : ListPage
{
    private readonly AwakeService _awakeService;

    public AwakeDashboardPage(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.dashboard";
        Title = "Awake Dashboard";
        Name = "Open";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override IListItem[] GetItems()
    {
        var status = _awakeService.GetStatus();
        var settings = _awakeService.GetSettings();
        var items = new List<IListItem>
        {
            new ListItem(new ToggleAwakeCommand(_awakeService))
            {
                Title = AwakeTime.FormatOverride(status.Override),
                Subtitle = status.HasActiveSchedule ? "Schedule is active" : "Manual/session state",
                Icon = status.Override is null && !status.HasActiveSchedule ? AwakeVisuals.Moon : AwakeVisuals.Power,
                Tags = [status.Override is null && !status.HasActiveSchedule ? AwakeVisuals.MutedTag("sleep allowed") : AwakeVisuals.StatusTag("active")],
            },
            new ListItem(new AwakeForPage(_awakeService))
            {
                Title = "Awake For...",
                Subtitle = $"Default: {settings.DefaultDurationMinutes} minutes",
                Icon = AwakeVisuals.Clock,
                Tags = [AwakeVisuals.MutedTag("type minutes")],
            },
            new ListItem(new AwakeUntilPage(_awakeService))
            {
                Title = "Awake Until",
                Subtitle = $"Default: {settings.DefaultUntilTime}",
                Icon = AwakeVisuals.Calendar,
                Tags = [AwakeVisuals.MutedTag("type time")],
            },
            new ListItem(new LetSleepCommand(_awakeService))
            {
                Title = "Let Sleep",
                Subtitle = "Cancel the active manual keep-awake session",
                Icon = AwakeVisuals.Moon,
            },
            new ListItem(new AwakeSchedulesPage(_awakeService))
            {
                Title = "Awake Schedules",
                Subtitle = $"{status.Schedules.Count(s => s.Enabled)}/{status.Schedules.Count} enabled",
                Icon = AwakeVisuals.Calendar,
                Tags = [status.Schedules.Count > 0 ? AwakeVisuals.StatusTag("configured") : AwakeVisuals.MutedTag("none")],
            },
            new ListItem(new SmartAwakePage(_awakeService))
            {
                Title = "Smart Awake",
                Subtitle = "Natural language duration, until, schedule, status, or stop",
                Icon = AwakeVisuals.Sparkle,
                Tags = [AwakeVisuals.MutedTag("type request")],
            },
            new ListItem(new NoOpCommand())
            {
                Title = status.DaemonPid is int pid ? $"Daemon running: PID {pid}" : "Daemon stopped",
                Subtitle = status.Heartbeat is null ? "No heartbeat yet" : $"Last heartbeat: {AwakeTime.FormatRemaining(status.Heartbeat.TimestampUtc + 60)} freshness",
                Icon = status.DaemonPid is null ? AwakeVisuals.Stop : AwakeVisuals.Check,
                Tags = [status.DaemonPid is null ? AwakeVisuals.MutedTag("stopped") : AwakeVisuals.StatusTag("running")],
            },
            new ListItem(new SetDefaultModeCommand(_awakeService, "indefinite", "Set Default Mode: Indefinite"))
            {
                Title = "Default Mode: Indefinite",
                Subtitle = settings.DefaultAwakeMode == "indefinite" ? "Current" : "Use for Awake toggle",
                Icon = AwakeVisuals.Power,
                Tags = [settings.DefaultAwakeMode == "indefinite" ? AwakeVisuals.StatusTag("current") : AwakeVisuals.MutedTag("option")],
            },
            new ListItem(new SetDefaultModeCommand(_awakeService, "screen-off", "Set Default Mode: Screen Off"))
            {
                Title = "Default Mode: Screen-Off",
                Subtitle = settings.DefaultAwakeMode == "screen-off" ? "Current" : "System stays awake, display may sleep",
                Icon = AwakeVisuals.Moon,
                Tags = [settings.DefaultAwakeMode == "screen-off" ? AwakeVisuals.StatusTag("current") : AwakeVisuals.MutedTag("option")],
            },
            new ListItem(new StopDaemonCommand(_awakeService))
            {
                Title = "Stop Awake Daemon",
                Subtitle = "Advanced: scheduled awake windows will not apply until daemon restarts",
                Icon = AwakeVisuals.Stop,
                Tags = [AwakeVisuals.WarningTag("advanced")],
            },
        };

        return items.ToArray();
    }
}
