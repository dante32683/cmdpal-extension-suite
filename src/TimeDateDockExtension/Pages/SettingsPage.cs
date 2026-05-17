using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension.Pages;

internal sealed partial class SettingsPage : ContentPage
{
    private readonly SettingsManager _settingsManager;

    public SettingsPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Id = "com.dziad.timedatedockextension.settings";
        Name = "Settings";
        Icon = new IconInfo("\uE713");
        Title = "Time Date Dock Settings";
    }

    public override IContent[] GetContent() => _settingsManager.Settings.ToContent();
}
