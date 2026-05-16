using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

internal sealed partial class SettingsPage : ContentPage
{
    private readonly SettingsManager _settingsManager;

    public SettingsPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Name  = "Settings";
        Icon  = new IconInfo("\uE713");
        Title = "Simple Analytics Settings";
    }

    public override IContent[] GetContent() => _settingsManager.Settings.ToContent();
}