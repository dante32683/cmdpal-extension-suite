using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

#pragma warning disable CA1001  // timer is process-lifetime; extension exits with the host
internal sealed partial class WifiDockPage : ListPage
{
    private const string IconWifiFull = "\uE701";
    private const string IconWifi1 = "\uE872";
    private const string IconWifi2 = "\uE873";
    private const string IconWifi3 = "\uE874";
    private const string IconNoWifi = "\uE871";
    private const string IconEthernet = "\uE839";

    private readonly NetworkService _network;
    private readonly ListItem _wifiItem;
    private readonly Timer _timer;

    private static readonly IconInfo[] _wifiIcons =
    [
        new(IconNoWifi),
        new(IconWifi1),
        new(IconWifi2),
        new(IconWifi3),
        new(IconWifiFull),
    ];

    private static readonly IconInfo _iconEthernet = new(IconEthernet);

    internal static readonly IconInfo AddBandIcon = new(IconWifiFull);

    public WifiDockPage(NetworkService network)
    {
        _network = network;

        Id = "com.dziad.simpleanalyticsextension.wifi";
        Name = "Wi-Fi";
        Icon = AddBandIcon;

        _wifiItem = new ListItem(new WifiPage(network))
        {
            Title = "Wi-Fi",
            Icon = AddBandIcon,
        };

        _timer = new Timer(Refresh, null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
    }

    public override IListItem[] GetItems() => [_wifiItem];

    private void Refresh(object? _)
    {
        try
        {
            var info = _network.GetNetworkInfo();

            if (!info.Connected && !info.IsLimited)
            {
                _wifiItem.Title = "Offline";
                _wifiItem.Icon = _wifiIcons[0];
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
                _wifiItem.Icon = _iconEthernet;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Simple Analytics network refresh failed: {ex}");
            _wifiItem.Title = "Network";
            _wifiItem.Icon = _wifiIcons[0];
        }
    }

    private static IconInfo WifiIcon(int bars) => _wifiIcons[Math.Clamp(bars, 0, 4)];
}
