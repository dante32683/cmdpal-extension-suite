using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

// One ListPage returned from GetDockBands(). The dock renders each ListItem
// as a separate button. Battery and WiFi items wrap their detail ListPage so
// clicking navigates into it; CPU wraps NoOpCommand (display-only readout).
#pragma warning disable CA1001  // timers are process-lifetime; extension exits with the host
internal sealed partial class StatusDockPage : ListPage
{
    private readonly BatteryService _battery;
    private readonly NetworkService _network;
    private readonly CpuService _cpu;
    private readonly SettingsManager _settings;

    private readonly ListItem _batteryItem;
    private readonly ListItem _wifiItem;
    private readonly ListItem _cpuItem;

    private readonly Timer _batteryTimer;
    private readonly Timer _wifiTimer;
    private readonly Timer _cpuTimer;

    // Segoe Fluent Icons: always explicit \uXXXX escapes, never paste glyphs.
    // MobBattery0-10: EBA0-EBAA  |  MobBatteryCharging0: EBAB
    private const string IconBattery0      = "\uEBA0"; // Battery0 (empty)
    private const string IconBatteryCharge = "\uEBAB"; // BatteryCharging0
    private const string IconWifiFull      = "\uE701"; // WiFi (full bars)
    private const string IconWifi1         = "\uE872"; // Wifi1 (weak)
    private const string IconWifi2         = "\uE873"; // Wifi2 (fair)
    private const string IconWifi3         = "\uE874"; // Wifi3 (good)
    private const string IconNoWifi        = "\uE871"; // SignalNotConnected
    private const string IconEthernet      = "\uE839"; // Ethernet
    private const string IconCpu           = "\uEEA1"; // CPU (chip)

    public StatusDockPage(BatteryService battery, NetworkService network, CpuService cpu, SettingsManager settings)
    {
        _battery  = battery;
        _network  = network;
        _cpu      = cpu;
        _settings = settings;
        _settings.Settings.SettingsChanged += SettingsOnSettingsChanged;

        Id   = "com.dziad.simpleanalyticsextension.statusdock";
        Name = "Status";

        // Detail pages ARE commands: clicking a dock button calls the page's
        // Invoke(), which causes the palette to navigate into that page.
        var batteryDetail = new BatteryPage(battery);
        var wifiDetail    = new WifiPage(network);

        _batteryItem = new ListItem(batteryDetail)
        {
            Title = "Battery",
            Icon  = new IconInfo(IconBattery0),
        };
        _wifiItem = new ListItem(wifiDetail)
        {
            Title = "WiFi",
            Icon  = new IconInfo(IconWifiFull),
        };
        _cpuItem = new ListItem(new NoOpCommand())
        {
            Title = "CPU",
            Icon  = new IconInfo(IconCpu),
        };

        // items are assembled dynamically in GetItems() to honour settings

        _batteryTimer = new Timer(RefreshBattery, null, TimeSpan.Zero,           TimeSpan.FromSeconds(30));
        _wifiTimer    = new Timer(RefreshWifi,    null, TimeSpan.Zero,           TimeSpan.FromSeconds(15));
        _cpuTimer     = new Timer(RefreshCpu,     null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>(3);
        if (_settings.ShowBattery) items.Add(_batteryItem);
        if (_settings.ShowWifi)    items.Add(_wifiItem);
        if (_settings.ShowCpu)     items.Add(_cpuItem);
        return [.. items];
    }

    private void SettingsOnSettingsChanged(object sender, Settings args)
    {
        RaiseItemsChanged();
    }

    // Battery

    private void RefreshBattery(object? _)
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
            _batteryItem.Icon = new IconInfo(IconBattery0);
        }
    }

    private static IconInfo BatteryIcon(BatteryInfo info)
    {
        if (!info.HasBattery) return new IconInfo(IconBattery0);
        // \uEBA0 (empty) to \uEBAA (full): one glyph per 10%.
        var level = Math.Clamp(info.Percent / 10, 0, 10);
        // MobBatteryCharging0-10: EBAB-EBB5 (level-matched so 100% charging shows full)
        if (info.IsCharging)
            return new IconInfo(((char)(0xEBAB + level)).ToString());

        // MobBattery0-10: EBA0-EBAA
        return new IconInfo(((char)(0xEBA0 + level)).ToString());
    }

    // WiFi

    private void RefreshWifi(object? _)
    {
        try
        {
            var info = _network.GetNetworkInfo();

            if (!info.Connected && !info.IsLimited)
            {
                _wifiItem.Title = "Offline";
                _wifiItem.Icon = new IconInfo(IconNoWifi);
                return;
            }

            if (info.IsWifi && !string.IsNullOrEmpty(info.Ssid))
            {
                _wifiItem.Title = info.Ssid.Length > 14 ? info.Ssid[..14] + "..." : info.Ssid;
                _wifiItem.Icon = WifiIcon(info.SignalBars);
            }
            else
            {
                _wifiItem.Title = "Wired";
                _wifiItem.Icon = new IconInfo(IconEthernet);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Simple Analytics network refresh failed: {ex}");
            _wifiItem.Title = "Network";
            _wifiItem.Icon = new IconInfo(IconNoWifi);
        }
    }

    private static IconInfo WifiIcon(int bars) => bars switch
    {
        0      => new IconInfo(IconNoWifi),
        1      => new IconInfo(IconWifi1),
        2      => new IconInfo(IconWifi2),
        3      => new IconInfo(IconWifi3),
        _      => new IconInfo(IconWifiFull),
    };

    // CPU

    private void RefreshCpu(object? _)
    {
        try
        {
            var pct = _cpu.GetCpuPercent();
            _cpuItem.Title = $"{pct:F0}%";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Simple Analytics CPU refresh failed: {ex}");
            _cpuItem.Title = "CPU";
        }
    }
}
