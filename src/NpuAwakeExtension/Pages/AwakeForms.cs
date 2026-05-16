using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Awake.Commands;
using NpuTools.Awake.Models;
using NpuTools.Awake.Services;

namespace NpuTools.Awake.Pages;

internal sealed partial class AwakeForPage : ListPage
{
    private readonly AwakeService _awakeService;

    public AwakeForPage(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.for";
        Title = "Awake For...";
        Name = "Open";
        Icon = AwakeVisuals.Clock;
        PlaceholderText = "Type minutes, then press Enter";
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();
        string query = SearchText?.Trim() ?? "";
        if (int.TryParse(query, out int minutes) && minutes > 0)
        {
            items.Add(new ListItem(new AwakeForMinutesCommand(_awakeService, minutes))
            {
                Title = $"Start for {minutes} minute(s)",
                Subtitle = "Press Enter to submit typed duration",
                Icon = AwakeVisuals.Clock,
                Tags = [AwakeVisuals.StatusTag("typed")],
            });
        }
        else if (!string.IsNullOrWhiteSpace(query))
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = "Type a positive number of minutes",
                Subtitle = $"Could not use: {query}",
                Icon = AwakeVisuals.Clock,
                Tags = [AwakeVisuals.WarningTag("invalid")],
            });
        }

        int[] presets = [15, 30, 60, 90, 120, 240];
        foreach (int preset in presets)
        {
            items.Add(new ListItem(new AwakeForMinutesCommand(_awakeService, preset))
            {
                Title = $"{preset} minutes",
                Subtitle = preset == _awakeService.GetSettings().DefaultDurationMinutes ? "Default duration" : "Preset duration",
                Icon = AwakeVisuals.Clock,
                Tags = [AwakeVisuals.MutedTag("preset")],
                TextToSuggest = preset.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        return items.ToArray();
    }
}

internal sealed partial class AwakeForForm : FormContent
{
    private readonly AwakeService _awakeService;

    public AwakeForForm(AwakeService awakeService)
    {
        _awakeService = awakeService;
        var settings = awakeService.GetSettings();
        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "text": "Awake For", "weight": "Bolder", "size": "Medium", "wrap": true },
    { "type": "Input.Number", "id": "minutes", "label": "Duration in minutes", "min": 1, "max": 1440, "value": {{settings.DefaultDurationMinutes}} }
  ],
  "actions": [{ "type": "Action.Submit", "title": "Start Awake" }]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        int minutes = ReadInt(payload, "minutes", _awakeService.GetSettings().DefaultDurationMinutes);
        if (minutes <= 0)
        {
            return CommandResult.ShowToast("Enter a positive duration.");
        }

        bool ok = _awakeService.SetOverride(new AwakeOverride { Mode = "timed", ExpiryEpochSeconds = AwakeTime.FromMinutes(minutes) });
        return CommandResult.ShowToast(ok ? $"PC will stay awake for {minutes} minute(s)." : "Awake keeper could not be started.");
    }

    private static int ReadInt(string payload, string name, int fallback)
    {
        try
        {
            string? raw = JsonNode.Parse(payload)?[name]?.ToString();
            return int.TryParse(raw, out int value) ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}

internal sealed partial class AwakeUntilPage : ListPage
{
    private readonly AwakeService _awakeService;

    public AwakeUntilPage(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.until";
        Title = "Awake Until";
        Name = "Open";
        Icon = AwakeVisuals.Calendar;
        PlaceholderText = "Type a time like 17:30, then press Enter";
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();
        string query = SearchText?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(query))
        {
            long? epoch = AwakeTime.ParseUntilToEpoch(query);
            items.Add(epoch is null
                ? new ListItem(new NoOpCommand())
                {
                    Title = "Use HH:mm, like 17:30",
                    Subtitle = $"Could not use: {query}",
                    Icon = AwakeVisuals.Calendar,
                    Tags = [AwakeVisuals.WarningTag("invalid")],
                }
                : new ListItem(new AwakeUntilTimeCommand(_awakeService, query))
                {
                    Title = $"Start until {query}",
                    Subtitle = "Press Enter to submit typed time",
                    Icon = AwakeVisuals.Calendar,
                    Tags = [AwakeVisuals.StatusTag("typed")],
                });
        }

        string[] presets = [_awakeService.GetSettings().DefaultUntilTime, "12:00", "17:00", "18:00", "22:00"];
        foreach (string preset in presets.Distinct())
        {
            items.Add(new ListItem(new AwakeUntilTimeCommand(_awakeService, preset))
            {
                Title = preset,
                Subtitle = "Preset local time",
                Icon = AwakeVisuals.Calendar,
                Tags = [AwakeVisuals.MutedTag("preset")],
                TextToSuggest = preset,
            });
        }

        return items.ToArray();
    }
}

internal sealed partial class AwakeUntilForm : FormContent
{
    private readonly AwakeService _awakeService;

    public AwakeUntilForm(AwakeService awakeService)
    {
        _awakeService = awakeService;
        string defaultTime = awakeService.GetSettings().DefaultUntilTime;
        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "text": "Awake Until", "weight": "Bolder", "size": "Medium", "wrap": true },
    { "type": "Input.Text", "id": "time", "label": "Local time", "placeholder": "17:30", "value": "{{defaultTime}}", "isRequired": true, "errorMessage": "Use HH:mm, like 17:30." }
  ],
  "actions": [{ "type": "Action.Submit", "title": "Start Awake" }]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        string time = ReadString(payload, "time", _awakeService.GetSettings().DefaultUntilTime);
        long? epoch = AwakeTime.ParseUntilToEpoch(time);
        if (epoch is null)
        {
            return CommandResult.ShowToast("Use HH:mm, like 17:30.");
        }

        bool ok = _awakeService.SetOverride(new AwakeOverride { Mode = "until", ExpiryEpochSeconds = epoch });
        return CommandResult.ShowToast(ok ? $"PC will stay awake until {time}." : "Awake keeper could not be started.");
    }

    private static string ReadString(string payload, string name, string fallback)
    {
        try
        {
            return JsonNode.Parse(payload)?[name]?.ToString() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}

internal sealed partial class AddSchedulePage : ContentPage
{
    private readonly AddScheduleForm _form;

    public AddSchedulePage(AwakeService awakeService)
    {
        _form = new AddScheduleForm(awakeService);
        Id = "com.local.nputools.awake.schedule.add";
        Title = "Add Schedule";
        Name = "Open";
        Icon = AwakeVisuals.Calendar;
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class AddScheduleForm : FormContent
{
    private readonly AwakeService _awakeService;

    public AddScheduleForm(AwakeService awakeService)
    {
        _awakeService = awakeService;
        var settings = awakeService.GetSettings();
        TemplateJson = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "text": "Add Awake Schedule", "weight": "Bolder", "size": "Medium", "wrap": true },
    { "type": "Input.Text", "id": "days", "label": "Days", "placeholder": "mon,tue,wed,thu,fri", "value": "{{AwakeTime.FormatDays(settings.DefaultScheduleDays).ToLowerInvariant()}}" },
    { "type": "Input.Text", "id": "start", "label": "Start", "placeholder": "09:00", "value": "{{settings.DefaultScheduleStart}}" },
    { "type": "Input.Text", "id": "end", "label": "End", "placeholder": "17:00", "value": "{{settings.DefaultScheduleEnd}}" }
  ],
  "actions": [{ "type": "Action.Submit", "title": "Save Schedule" }]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        string daysText = ReadString(payload, "days", "mon,tue,wed,thu,fri");
        string start = ReadString(payload, "start", _awakeService.GetSettings().DefaultScheduleStart);
        string end = ReadString(payload, "end", _awakeService.GetSettings().DefaultScheduleEnd);
        int[] days = ParseDays(daysText);

        if (days.Length == 0 || !AwakeTime.TryParseHourMinute(start, out _) || !AwakeTime.TryParseHourMinute(end, out _))
        {
            return CommandResult.ShowToast("Use days like mon,tue and times like 09:00.");
        }

        _awakeService.AddSchedule(new AwakeSchedule
        {
            Id = $"sched_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Enabled = true,
            Days = days,
            Start = start,
            End = end,
        });

        return CommandResult.ShowToast($"Schedule saved: {AwakeTime.FormatDays(days)} {start}-{end}.");
    }

    private static int[] ParseDays(string value)
    {
        if (value.Contains("weekday", StringComparison.OrdinalIgnoreCase))
        {
            return [1, 2, 3, 4, 5];
        }

        if (value.Contains("weekend", StringComparison.OrdinalIgnoreCase))
        {
            return [0, 6];
        }

        var days = new List<int>();
        string[] labels = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
        for (int i = 0; i < labels.Length; i++)
        {
            if (value.Contains(labels[i], StringComparison.OrdinalIgnoreCase))
            {
                days.Add(i);
            }
        }

        return days.Distinct().Order().ToArray();
    }

    private static string ReadString(string payload, string name, string fallback)
    {
        try
        {
            return JsonNode.Parse(payload)?[name]?.ToString() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}

internal sealed partial class SmartAwakePage : ListPage
{
    private readonly AwakeService _awakeService;

    public SmartAwakePage(AwakeService awakeService)
    {
        _awakeService = awakeService;
        Id = "com.local.nputools.awake.smart";
        Title = "Smart Awake";
        Name = "Open";
        Icon = AwakeVisuals.Sparkle;
        PlaceholderText = "Type an Awake request, then press Enter";
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();
        string query = SearchText?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(query))
        {
            items.Add(new ListItem(new SmartAwakeQueryCommand(_awakeService, query))
            {
                Title = $"Run: {query}",
                Subtitle = "Press Enter to submit typed request",
                Icon = AwakeVisuals.Sparkle,
                Tags = [AwakeVisuals.StatusTag("typed")],
            });
        }

        string[] examples =
        [
            "keep awake",
            "keep awake for 90 minutes",
            "until 17:30",
            "weekdays 09:00 to 17:00",
            "stop schedules",
            "status",
        ];

        foreach (string example in examples)
        {
            items.Add(new ListItem(new SmartAwakeQueryCommand(_awakeService, example))
            {
                Title = example,
                Subtitle = "Example request",
                Icon = AwakeVisuals.Sparkle,
                Tags = [AwakeVisuals.MutedTag("example")],
                TextToSuggest = example,
            });
        }

        return items.ToArray();
    }
}

internal sealed partial class SmartAwakeForm : FormContent
{
    private readonly AwakeService _awakeService;
    private readonly SmartAwakeService _smartAwakeService = new();

    public SmartAwakeForm(AwakeService awakeService)
    {
        _awakeService = awakeService;
        TemplateJson = """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    { "type": "TextBlock", "text": "Smart Awake", "weight": "Bolder", "size": "Medium", "wrap": true },
    { "type": "TextBlock", "text": "Examples: keep awake for 90 minutes, until 17:30, weekdays 09:00 to 17:00, stop schedules.", "wrap": true },
    { "type": "Input.Text", "id": "text", "label": "Request", "isMultiline": true, "isRequired": true, "errorMessage": "Enter what you want." }
  ],
  "actions": [{ "type": "Action.Submit", "title": "Run" }]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        string text = ReadString(payload, "text", "");
        var result = _smartAwakeService.Execute(text, _awakeService);
        return CommandResult.ShowToast(result.Message);
    }

    private static string ReadString(string payload, string name, string fallback)
    {
        try
        {
            return JsonNode.Parse(payload)?[name]?.ToString() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
