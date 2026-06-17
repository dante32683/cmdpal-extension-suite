using System.Collections.Generic;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace TimeDateDockExtension.Services;

internal sealed class SettingsManager : JsonSettingsManager
{
    private bool _timeUses24Hour;
    private bool _timeLeadingZero;
    private bool _timeShowSeconds;
    private string _customTimeFormat = string.Empty;
    private string _dateOrder = "mdy";
    private bool _dateMonthLeadingZero;
    private bool _dateDayLeadingZero;
    private bool _dateFourDigitYear;
    private string _customDateFormat = string.Empty;

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

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
        UpdateCachedSettings();
        Settings.SettingsChanged += (_, _) =>
        {
            SaveSettings();
            UpdateCachedSettings();
        };
    }

    private void UpdateCachedSettings()
    {
        _timeUses24Hour = GetString("timeClock", "24") == "24";
        _timeLeadingZero = Settings.GetSetting<bool>("timeLeadingZero");
        _timeShowSeconds = Settings.GetSetting<bool>("timeShowSeconds");
        _customTimeFormat = GetString("customTimeFormat", string.Empty).Trim();
        _dateOrder = GetString("dateOrder", "mdy");
        _dateMonthLeadingZero = Settings.GetSetting<bool>("dateMonthLeadingZero");
        _dateDayLeadingZero = Settings.GetSetting<bool>("dateDayLeadingZero");
        _dateFourDigitYear = Settings.GetSetting<bool>("dateFourDigitYear");
        _customDateFormat = GetString("customDateFormat", string.Empty).Trim();
    }

    public bool TimeUses24Hour => _timeUses24Hour;
    public bool TimeLeadingZero => _timeLeadingZero;
    public bool TimeShowSeconds => _timeShowSeconds;
    public string CustomTimeFormat => _customTimeFormat;
    public string DateOrder => _dateOrder;
    public bool DateMonthLeadingZero => _dateMonthLeadingZero;
    public bool DateDayLeadingZero => _dateDayLeadingZero;
    public bool DateFourDigitYear => _dateFourDigitYear;
    public string CustomDateFormat => _customDateFormat;

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
