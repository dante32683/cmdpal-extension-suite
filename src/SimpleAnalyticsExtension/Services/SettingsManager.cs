using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

internal sealed class SettingsManager : JsonSettingsManager
{
    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(new ToggleSetting("showBattery", true)
        {
            Label = "Show Battery",
            Description = "Show battery percentage in the dock strip",
        });
        Settings.Add(new ToggleSetting("showWifi", true)
        {
            Label = "Show Wi-Fi",
            Description = "Show connected Wi-Fi network in the dock strip",
        });
        Settings.Add(new ToggleSetting("showCpu", true)
        {
            Label = "Show CPU",
            Description = "Show CPU usage percentage in the dock strip",
        });

        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    public bool ShowBattery => Settings.GetSetting<bool>("showBattery");
    public bool ShowWifi    => Settings.GetSetting<bool>("showWifi");
    public bool ShowCpu     => Settings.GetSetting<bool>("showCpu");

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "simpleAnalytics.settings.json");
    }
}
