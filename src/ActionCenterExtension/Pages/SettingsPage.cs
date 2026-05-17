using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ActionCenterExtension.Pages;

#pragma warning disable CA1001  // SDK owns page lifetime; IDisposable not called reliably
internal sealed partial class SettingsPage : ContentPage
{
    private readonly SettingsManager _settingsManager;

    public SettingsPage(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        Id = "com.dziad.actioncenterextension.settings";
        Name = "Settings";
        Icon = new IconInfo("\uE713");
        Title = "Action Center Settings";
        _settingsManager.Settings.SettingsChanged += OnSettingsChanged;
    }

    public override IContent[] GetContent() => _settingsManager.Settings.ToContent();

    private void OnSettingsChanged(object sender, Settings args)
    {
        RaiseItemsChanged();
    }
}