using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Commands;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension.Pages;

internal sealed partial class DateDockPage : ListPage, IDisposable
{
    private static readonly IconInfo DateIcon = new("\uE787");

    private readonly SettingsManager _settingsManager;
    private readonly ListItem _dateItem;
    private readonly Timer _timer;

    internal static IconInfo AddBandIcon => DateIcon;

    public DateDockPage(SettingsManager settingsManager, NotificationCenterService notificationCenter)
    {
        _settingsManager = settingsManager;
        _settingsManager.Settings.SettingsChanged += SettingsOnSettingsChanged;

        Id = "com.dziad.timedatedockextension.dock.date";
        Name = "Date";
        Icon = DateIcon;

        _dateItem = new ListItem(new NotificationCenterCommand(
            "com.dziad.timedatedockextension.dock.date.open",
            "Open Notification Center",
            null,
            notificationCenter))
        {
            Title = "Date",
        };

        UpdateItem();
        _timer = new Timer(Refresh, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public override IListItem[] GetItems() => [_dateItem];

    public void Dispose()
    {
        _settingsManager.Settings.SettingsChanged -= SettingsOnSettingsChanged;
        _timer.Dispose();
    }

    private void SettingsOnSettingsChanged(object sender, Settings args)
    {
        UpdateItem();
        RaiseItemsChanged();
    }

    private void Refresh(object? _)
    {
        try
        {
            UpdateItem();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Time Date date dock refresh failed: {ex}");
        }
    }

    private void UpdateItem()
    {
        _dateItem.Title = TimeDateFormatService.FormatDate(DateTime.Now, _settingsManager);
    }
}
