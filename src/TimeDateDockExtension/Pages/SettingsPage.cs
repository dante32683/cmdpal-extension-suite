using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension.Pages;

#pragma warning disable CA1001  // SDK owns page lifetime; IDisposable not called reliably
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
        _settingsManager.Settings.SettingsChanged += OnSettingsChanged;
    }

    public override IContent[] GetContent() => _settingsManager.Settings.ToContent();

    private void OnSettingsChanged(object sender, Settings args) => RaiseItemsChanged();
}
