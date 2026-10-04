using System;
using ActionCenterExtension.Bands;
using ActionCenterExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ActionCenterExtension;

public sealed partial class ActionCenterExtensionCommandsProvider : CommandProvider
{
    private static readonly IconInfo ProviderIcon = new("");
    private readonly SettingsManager _settingsManager = new();
    private readonly VirtualDesktopPage _virtualDesktopPage = new(new VirtualDesktopReader());
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _dockBands;

    public ActionCenterExtensionCommandsProvider()
    {
        Id = "com.dziad.actioncenterextension";
        DisplayName = "Action Center";
        Icon = ProviderIcon;
        Settings = _settingsManager.Settings;

        _commands = [
            new CommandItem(new ActionCenterExtensionPage(_settingsManager)) { Title = DisplayName, Subtitle = "Open Action Center settings and dock controls", Icon = Icon },
        ];

        var quickSettings = new QuickSettingsCommand(_settingsManager);
        _dockBands = [
            new CommandItem(quickSettings) { Title = "Quick Settings", Icon = quickSettings.Icon },
            new CommandItem(_virtualDesktopPage)
            {
                Title = "Virtual Desktop",
                Subtitle = "Name of the active virtual desktop",
                Icon = VirtualDesktopPage.AddBandIcon,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => _dockBands;

    public override void Dispose()
    {
        base.Dispose();
        _virtualDesktopPage.Dispose();
    }
}
