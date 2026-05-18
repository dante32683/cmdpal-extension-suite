using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Pages;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension;

public sealed partial class TimeDateDockCommandsProvider : CommandProvider, System.IDisposable
{
    private readonly SettingsManager _settingsManager = new();
    private readonly NotificationCenterService _notificationCenter = new();
    private readonly TimeDockPage _timeDockPage;
    private readonly DateDockPage _dateDockPage;
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _dockBands;

    public TimeDateDockCommandsProvider()
    {
        Id = "com.dziad.timedatedockextension";
        DisplayName = "Time Date Dock";
        Icon = new IconInfo("\uE916");
        Settings = _settingsManager.Settings;

        _timeDockPage = new TimeDockPage(_settingsManager, _notificationCenter);
        _dateDockPage = new DateDockPage(_settingsManager, _notificationCenter);
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
            new CommandItem(_timeDockPage)
            {
                Title = "Time",
                Icon = TimeDockPage.AddBandIcon,
            },
            new CommandItem(_dateDockPage)
            {
                Title = "Date",
                Icon = DateDockPage.AddBandIcon,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => _dockBands;

    public override void Dispose()
    {
        base.Dispose();
        _timeDockPage.Dispose();
        _dateDockPage.Dispose();
        _notificationCenter.Dispose();
    }
}
