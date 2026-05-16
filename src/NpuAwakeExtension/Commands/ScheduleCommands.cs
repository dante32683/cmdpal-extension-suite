using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Commands;

internal sealed partial class ToggleScheduleCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;
    private readonly string _scheduleId;

    public ToggleScheduleCommand(AwakeService awakeService, string scheduleId)
    {
        _awakeService = awakeService;
        _scheduleId = scheduleId;
        Id = $"com.local.nputools.awake.schedule.toggle.{scheduleId}";
        Name = "Pause or Resume Schedule";
        Icon = AwakeVisuals.Calendar;
    }

    public override CommandResult Invoke()
    {
        _awakeService.ToggleSchedule(_scheduleId);
        return CommandResult.ShowToast("Schedule updated.");
    }
}

internal sealed partial class DeleteScheduleCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;
    private readonly string _scheduleId;

    public DeleteScheduleCommand(AwakeService awakeService, string scheduleId)
    {
        _awakeService = awakeService;
        _scheduleId = scheduleId;
        Id = $"com.local.nputools.awake.schedule.delete.{scheduleId}";
        Name = "Delete Schedule";
        Icon = AwakeVisuals.Stop;
    }

    public override CommandResult Invoke()
    {
        _awakeService.DeleteSchedule(_scheduleId);
        return CommandResult.ShowToast("Schedule deleted.");
    }
}

internal sealed partial class ClearSchedulesCommand : InvokableCommand
{
    private readonly AwakeService _awakeService;

    public ClearSchedulesCommand(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.schedule.clear";
        Name = "Clear All Schedules";
        Icon = AwakeVisuals.Stop;
    }

    public override CommandResult Invoke()
    {
        _awakeService.SetSchedules([]);
        return CommandResult.ShowToast("Schedules cleared.");
    }
}
