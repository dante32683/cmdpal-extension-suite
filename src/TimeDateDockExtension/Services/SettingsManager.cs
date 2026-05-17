using System.Collections.Generic;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace TimeDateDockExtension.Services;

internal sealed class SettingsManager : JsonSettingsManager
{
    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(new ToggleSetting("showTime", true)
        {
            Label = "Show time dock item",
            Description = "Show the formatted time as a separate dock button",
        });
        Settings.Add(new ToggleSetting("showDate", true)
        {
            Label = "Show date dock item",
            Description = "Show the formatted date as a separate dock button",
        });

        Settings.Add(new ChoiceSetSetting("timeClock", ClockChoices())
        {
            Label = "Time clock",
            Description = "Use 24-hour or 12-hour time for the time dock item",
        });
        Settings.Add(new ToggleSetting("timeLeadingZero", true)
        {
            Label = "Time leading zero",
            Description = "Show 09:05 instead of 9:05",
        });
        Settings.Add(new ToggleSetting("timeShowSeconds", false)
        {
            Label = "Time seconds",
            Description = "Include seconds in the time dock item",
        });
        Settings.Add(new TextSetting("customTimeFormat", string.Empty)
        {
            Label = "Custom time format",
            Description = "Optional .NET time format. Leave empty to use the time controls above",
        });

        Settings.Add(new ChoiceSetSetting("dateOrder", DateOrderChoices())
        {
            Label = "Date order",
            Description = "Choose the field order for the date dock item",
        });
        Settings.Add(new ToggleSetting("dateMonthLeadingZero", false)
        {
            Label = "Date month leading zero",
            Description = "Show 05 instead of 5 for the month",
        });
        Settings.Add(new ToggleSetting("dateDayLeadingZero", true)
        {
            Label = "Date day leading zero",
            Description = "Show 07 instead of 7 for the day",
        });
        Settings.Add(new ToggleSetting("dateFourDigitYear", true)
        {
            Label = "Date four digit year",
            Description = "Show 2026 instead of 26",
        });
        Settings.Add(new TextSetting("customDateFormat", string.Empty)
        {
            Label = "Custom date format",
            Description = "Optional .NET date format. Leave empty to use the date controls above",
        });

        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    public bool ShowTime => Settings.GetSetting<bool>("showTime");
    public bool ShowDate => Settings.GetSetting<bool>("showDate");
    public bool TimeUses24Hour => GetString("timeClock", "24") == "24";
    public bool TimeLeadingZero => Settings.GetSetting<bool>("timeLeadingZero");
    public bool TimeShowSeconds => Settings.GetSetting<bool>("timeShowSeconds");
    public string CustomTimeFormat => GetString("customTimeFormat", string.Empty).Trim();
    public string DateOrder => GetString("dateOrder", "mdy");
    public bool DateMonthLeadingZero => Settings.GetSetting<bool>("dateMonthLeadingZero");
    public bool DateDayLeadingZero => Settings.GetSetting<bool>("dateDayLeadingZero");
    public bool DateFourDigitYear => Settings.GetSetting<bool>("dateFourDigitYear");
    public string CustomDateFormat => GetString("customDateFormat", string.Empty).Trim();

    private string GetString(string key, string fallback)
    {
        var raw = Settings.GetSetting<string>(key);
        return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
    }

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "timeDateDock.settings.json");
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
