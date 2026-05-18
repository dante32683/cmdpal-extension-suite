using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using TimeDateDockExtension.Commands;
using TimeDateDockExtension.Services;

namespace TimeDateDockExtension.Pages;

internal sealed partial class TimeDockPage : ListPage, IDisposable
{
    private static readonly IconInfo TimeIcon = new("\uE916");

    private readonly SettingsManager _settingsManager;
    private readonly ListItem _timeItem;
    private readonly Timer _timer;

    internal static IconInfo AddBandIcon => TimeIcon;

    public TimeDockPage(SettingsManager settingsManager, NotificationCenterService notificationCenter)
    {
        _settingsManager = settingsManager;
        _settingsManager.Settings.SettingsChanged += SettingsOnSettingsChanged;

        Id = "com.dziad.timedatedockextension.dock.time";
        Name = "Time";
        Icon = TimeIcon;

        _timeItem = new ListItem(new NotificationCenterCommand(
            "com.dziad.timedatedockextension.dock.time.open",
            "Open Notification Center",
            null,
            notificationCenter))
        {
            Title = "Time",
        };

        UpdateItem();
        _timer = new Timer(Refresh, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public override IListItem[] GetItems() => [_timeItem];

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
            Debug.WriteLine($"Time Date time dock refresh failed: {ex}");
        }
    }

    private void UpdateItem()
    {
        _timeItem.Title = TimeDateFormatService.FormatTime(DateTime.Now, _settingsManager);
    }
}
