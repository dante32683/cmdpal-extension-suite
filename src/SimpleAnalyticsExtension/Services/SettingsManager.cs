using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

internal sealed class SettingsManager
{
    private readonly Settings _settings = new();

    public SettingsManager()
    {
        _settings.Add(new ToggleSetting("showBattery", true)
        {
            Label = "Show Battery",
            Description = "Show battery percentage in the dock strip",
        });
        _settings.Add(new ToggleSetting("showWifi", true)
        {
            Label = "Show Wi-Fi",
            Description = "Show connected Wi-Fi network in the dock strip",
        });
        _settings.Add(new ToggleSetting("showCpu", true)
        {
            Label = "Show CPU",
            Description = "Show CPU usage percentage in the dock strip",
        });
    }

    public Settings Settings => _settings;

    public bool ShowBattery => _settings.GetSetting<bool>("showBattery");
    public bool ShowWifi    => _settings.GetSetting<bool>("showWifi");
    public bool ShowCpu     => _settings.GetSetting<bool>("showCpu");
}
