using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Commands;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension.Pages;

internal sealed partial class TimeDateDockPage : ListPage, IDisposable
{
    private readonly SettingsManager _settingsManager;
    private readonly ListItem _timeItem;
    private readonly ListItem _dateItem;
    private readonly Timer _timer;

    public TimeDateDockPage(SettingsManager settingsManager, NotificationCenterService notificationCenter)
    {
        _settingsManager = settingsManager;
        Id = "com.dziad.timedatedockextension.dock";
        Name = "Time Date";

        _timeItem = new ListItem(new NotificationCenterCommand(
            "com.dziad.timedatedockextension.dock.time",
            "Open Notification Center",
            null,
            notificationCenter))
        {
            Title = "Time",
        };

        _dateItem = new ListItem(new NotificationCenterCommand(
            "com.dziad.timedatedockextension.dock.date",
            "Open Notification Center",
            null,
            notificationCenter))
        {
            Title = "Date",
        };

        UpdateItems();
        _timer = new Timer(Refresh, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>(2);
        if (_settingsManager.ShowTime)
        {
            items.Add(_timeItem);
        }

        if (_settingsManager.ShowDate)
        {
            items.Add(_dateItem);
        }

        return [.. items];
    }

    public void Dispose() => _timer.Dispose();

    private void Refresh(object? _)
    {
        try
        {
            UpdateItems();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Time Date dock refresh failed: {ex}");
        }
    }

    private void UpdateItems()
    {
        var now = DateTime.Now;

        _timeItem.Title = TimeDateFormatService.FormatTime(now, _settingsManager);

        _dateItem.Title = TimeDateFormatService.FormatDate(now, _settingsManager);
    }
}
