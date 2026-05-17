using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace TimeDateDockExtension.Services;

internal sealed class SettingsManager
{
    private readonly Settings _settings = new();

    public SettingsManager()
    {
        _settings.Add(new ToggleSetting("showTime", true)
        {
            Label = "Show time dock item",
            Description = "Show the formatted time as a separate dock button",
        });
        _settings.Add(new ToggleSetting("showDate", true)
        {
            Label = "Show date dock item",
            Description = "Show the formatted date as a separate dock button",
        });

        _settings.Add(new ChoiceSetSetting("timeClock", ClockChoices())
        {
            Label = "Time clock",
            Description = "Use 24-hour or 12-hour time for the time dock item",
        });
        _settings.Add(new ToggleSetting("timeLeadingZero", true)
        {
            Label = "Time leading zero",
            Description = "Show 09:05 instead of 9:05",
        });
        _settings.Add(new ToggleSetting("timeShowSeconds", false)
        {
            Label = "Time seconds",
            Description = "Include seconds in the time dock item",
        });
        _settings.Add(new TextSetting("customTimeFormat", string.Empty)
        {
            Label = "Custom time format",
            Description = "Optional .NET time format. Leave empty to use the time controls above",
        });

        _settings.Add(new ChoiceSetSetting("dateOrder", DateOrderChoices())
        {
            Label = "Date order",
            Description = "Choose the field order for the date dock item",
        });
        _settings.Add(new ToggleSetting("dateMonthLeadingZero", false)
        {
            Label = "Date month leading zero",
            Description = "Show 05 instead of 5 for the month",
        });
        _settings.Add(new ToggleSetting("dateDayLeadingZero", true)
        {
            Label = "Date day leading zero",
            Description = "Show 07 instead of 7 for the day",
        });
        _settings.Add(new ToggleSetting("dateFourDigitYear", true)
        {
            Label = "Date four digit year",
            Description = "Show 2026 instead of 26",
        });
        _settings.Add(new TextSetting("customDateFormat", string.Empty)
        {
            Label = "Custom date format",
            Description = "Optional .NET date format. Leave empty to use the date controls above",
        });
    }

    public Settings Settings => _settings;

    public bool ShowTime => _settings.GetSetting<bool>("showTime");
    public bool ShowDate => _settings.GetSetting<bool>("showDate");
    public bool TimeUses24Hour => GetString("timeClock", "24") == "24";
    public bool TimeLeadingZero => _settings.GetSetting<bool>("timeLeadingZero");
    public bool TimeShowSeconds => _settings.GetSetting<bool>("timeShowSeconds");
    public string CustomTimeFormat => GetString("customTimeFormat", string.Empty).Trim();
    public string DateOrder => GetString("dateOrder", "mdy");
    public bool DateMonthLeadingZero => _settings.GetSetting<bool>("dateMonthLeadingZero");
    public bool DateDayLeadingZero => _settings.GetSetting<bool>("dateDayLeadingZero");
    public bool DateFourDigitYear => _settings.GetSetting<bool>("dateFourDigitYear");
    public string CustomDateFormat => GetString("customDateFormat", string.Empty).Trim();

    private string GetString(string key, string fallback)
    {
        var raw = _settings.GetSetting<string>(key);
        return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
    }

    private static List<ChoiceSetSetting.Choice> ClockChoices() =>
    [
        new("24-hour", "24"),
        new("12-hour", "12"),
    ];

    private static List<ChoiceSetSetting.Choice> DateOrderChoices() =>
    [
        new("Month / Day / Year", "mdy"),
        new("Day / Month / Year", "dmy"),
        new("Year - Month - Day", "ymd"),
    ];
}
