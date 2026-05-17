using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ActionCenterExtension;

internal sealed class SettingsManager : JsonSettingsManager
{
    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        var choices = new List<ChoiceSetSetting.Choice>
        {
            new("5 seconds",  "5"),
            new("10 seconds", "10"),
            new("15 seconds", "15"),
            new("30 seconds", "30"),
            new("60 seconds", "60"),
            new("Never",      "0"),
        };

        Settings.Add(new ChoiceSetSetting("quickSettingsCooldown", choices)
        {
            Label = "Quick Settings state reset",
            Description = "How long after opening Quick Settings before the button resets its open/closed state",
        });

        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    public TimeSpan? QuickSettingsCooldown
    {
        get
        {
            var raw = Settings.GetSetting<string>("quickSettingsCooldown");
            if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out int seconds))
                seconds = 10; // default to 10s if unset
            return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
        }
    }

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "actionCenter.settings.json");
    }
}
