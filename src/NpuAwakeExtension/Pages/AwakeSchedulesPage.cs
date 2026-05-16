using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Commands;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Pages;

internal sealed partial class AwakeSchedulesPage : ListPage
{
    private readonly AwakeService _awakeService;

    public AwakeSchedulesPage(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.schedules";
        Title = "Awake Schedules";
        Name = "Open";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
    }

    public override IListItem[] GetItems()
    {
        var schedules = _awakeService.GetSchedules();
        var items = new List<IListItem>
        {
            new ListItem(new AddSchedulePage(_awakeService))
            {
                Title = "Add Schedule",
                Subtitle = "Create a recurring awake window",
                Icon = AwakeVisuals.Calendar,
                Tags = [AwakeVisuals.StatusTag("new")],
            },
        };

        foreach (var schedule in schedules)
        {
            string status = schedule.Enabled ? "Enabled" : "Paused";
            if (AwakeTime.IsScheduleActiveNow(schedule, DateTimeOffset.Now))
            {
                status = "Active now";
            }

            items.Add(new ListItem(new ToggleScheduleCommand(_awakeService, schedule.Id))
            {
                Title = $"{schedule.Start}-{schedule.End}",
                Subtitle = $"{AwakeTime.FormatDays(schedule.Days)} - {status}",
                Icon = schedule.Enabled ? AwakeVisuals.Check : AwakeVisuals.Moon,
                Tags = [status == "Active now" ? AwakeVisuals.StatusTag("active") : schedule.Enabled ? AwakeVisuals.MutedTag("enabled") : AwakeVisuals.WarningTag("paused")],
            });

            items.Add(new ListItem(new DeleteScheduleCommand(_awakeService, schedule.Id))
            {
                Title = $"Delete {schedule.Start}-{schedule.End}",
                Subtitle = AwakeTime.FormatDays(schedule.Days),
                Icon = AwakeVisuals.Stop,
                Tags = [AwakeVisuals.WarningTag("delete")],
            });
        }

        if (schedules.Count > 0)
        {
            items.Add(new ListItem(new ClearSchedulesCommand(_awakeService))
            {
                Title = "Clear All Schedules",
                Subtitle = "Remove every recurring awake window",
                Icon = AwakeVisuals.Stop,
                Tags = [AwakeVisuals.WarningTag("destructive")],
            });
        }

        return items.ToArray();
    }
}
