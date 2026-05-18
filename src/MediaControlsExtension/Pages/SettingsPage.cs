// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.MediaControlsExtension.Pages;

#pragma warning disable CA1001 // Command Palette owns page lifetime for this process-lifetime settings page.
internal sealed partial class SettingsPage : ContentPage
{
    private readonly SettingsManager _settingsManager;

    public SettingsPage(SettingsManager settingsManager)
    {
        ArgumentNullException.ThrowIfNull(settingsManager);

        this._settingsManager = settingsManager;
        this._settingsManager.Settings.SettingsChanged += this.SettingsOnSettingsChanged;

        this.Id = "com.dziad.mediacontrolsextension.settings";
        this.Name = "Settings";
        this.Icon = new IconInfo("\uE713");
        this.Title = "Media Controls Settings";
    }

    public override IContent[] GetContent() => this._settingsManager.Settings.ToContent();

    private void SettingsOnSettingsChanged(object sender, Settings args)
    {
        this.RaiseItemsChanged();
    }
}
#pragma warning restore CA1001
