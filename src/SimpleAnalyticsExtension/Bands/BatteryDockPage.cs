using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

#pragma warning disable CA1001  // timer is process-lifetime; extension exits with the host
internal sealed partial class BatteryDockPage : ListPage
{
    private readonly BatteryService _battery;
    private readonly ListItem _batteryItem;
    private readonly Timer _timer;

    private static readonly IconInfo[] _batteryIcons;
    private static readonly IconInfo[] _chargingIcons;

    internal static readonly IconInfo AddBandIcon = new("\uEBAA");

    static BatteryDockPage()
    {
        _batteryIcons = new IconInfo[11];
        _chargingIcons = new IconInfo[11];

        for (int i = 0; i <= 10; i++)
        {
            _batteryIcons[i] = new IconInfo(((char)(0xEBA0 + i)).ToString());
            _chargingIcons[i] = new IconInfo(((char)(0xEBAB + i)).ToString());
        }
    }

    public BatteryDockPage(BatteryService battery)
    {
        _battery = battery;

        Id = "com.dziad.simpleanalyticsextension.battery";
        Name = "Battery";
        Icon = AddBandIcon;

        _batteryItem = new ListItem(new BatteryPage(battery))
        {
            Title = "Battery",
            Icon = _batteryIcons[0],
        };

        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public override IListItem[] GetItems() => [_batteryItem];

    private void Refresh(object? _)
    {
        try
        {
            var info = _battery.GetBatteryInfo();
            _batteryItem.Title = info.HasBattery ? $"{info.Percent}%" : "-";
            _batteryItem.Icon = BatteryIcon(info);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Simple Analytics battery refresh failed: {ex}");
            _batteryItem.Title = "Battery";
            _batteryItem.Icon = _batteryIcons[0];
        }
    }

    private static IconInfo BatteryIcon(BatteryInfo info)
    {
        if (!info.HasBattery)
        {
            return _batteryIcons[0];
        }

        var level = Math.Clamp(info.Percent / 10, 0, 10);
        return info.IsCharging ? _chargingIcons[level] : _batteryIcons[level];
    }
}
