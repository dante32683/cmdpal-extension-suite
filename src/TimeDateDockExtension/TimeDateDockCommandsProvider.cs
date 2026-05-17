using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Pages;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension;

public sealed partial class TimeDateDockCommandsProvider : CommandProvider, System.IDisposable
{
    private readonly SettingsManager _settingsManager = new();
    private readonly NotificationCenterService _notificationCenter = new();
    private readonly TimeDateDockPage _dockPage;
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _dockBands;

    public TimeDateDockCommandsProvider()
    {
        Id = "com.dziad.timedatedockextension";
        DisplayName = "Time Date Dock";
        Icon = new IconInfo("\uE121");
        Settings = _settingsManager.Settings;

        _dockPage = new TimeDateDockPage(_settingsManager, _notificationCenter);
        _commands =
        [
            new CommandItem(new TimeDateExtensionPage(_settingsManager, _notificationCenter))
            {
                Title = DisplayName,
                Icon = Icon,
            },
        ];

        _dockBands =
        [
            new CommandItem(_dockPage)
            {
                Title = "Time Date Dock",
                Icon = Icon,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => _dockBands;

    public override void Dispose()
    {
        base.Dispose();
        _dockPage.Dispose();
        _notificationCenter.Dispose();
    }
}
