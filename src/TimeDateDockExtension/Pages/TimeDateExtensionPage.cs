using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Commands;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension.Pages;

internal sealed partial class TimeDateExtensionPage : ListPage
{
    private readonly SettingsManager _settingsManager;
    private readonly NotificationCenterService _notificationCenter;

    public TimeDateExtensionPage(SettingsManager settingsManager, NotificationCenterService notificationCenter)
    {
        _settingsManager = settingsManager;
        _notificationCenter = notificationCenter;
        Id = "com.dziad.timedatedockextension.main";
        Icon = new IconInfo("\uE916");
        Title = "Time Date Dock";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        var now = DateTime.Now;

        return
        [
            new ListItem(new NotificationCenterCommand(
                "com.dziad.timedatedockextension.preview.time",
                "Open Notification Center",
                "\uE916",
                _notificationCenter))
            {
                Title = TimeDateFormatService.FormatTime(now, _settingsManager),
                Subtitle = $"Time format: {TimeDateFormatService.TimeFormat(_settingsManager)}",
                Icon = new IconInfo("\uE916"),
            },
            new ListItem(new NotificationCenterCommand(
                "com.dziad.timedatedockextension.preview.date",
                "Open Notification Center",
                "\uE787",
                _notificationCenter))
            {
                Title = TimeDateFormatService.FormatDate(now, _settingsManager),
                Subtitle = $"Date format: {TimeDateFormatService.DateFormat(_settingsManager)}",
                Icon = new IconInfo("\uE787"),
            },
            new ListItem(new SettingsPage(_settingsManager))
            {
                Title = "Settings",
                Subtitle = "Configure the time and date dock formats",
                Icon = new IconInfo("\uE713"),
            },
        ];
    }
}
