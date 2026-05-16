using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

internal sealed partial class WifiPage : ListPage
{
    private readonly NetworkService _service;

    // Segoe Fluent Icons -- always explicit \uXXXX escapes, never paste glyphs
    private const string IconWifiFull = "\uE701"; // WiFi full
    private const string IconWifi1    = "\uE872"; // Wifi1 weak
    private const string IconWifi2    = "\uE873"; // Wifi2 fair
    private const string IconWifi3    = "\uE874"; // Wifi3 good
    private const string IconNoWifi   = "\uE871"; // SignalNotConnected
    private const string IconEthernet = "\uE839"; // Ethernet
    private const string IconDownload = "\uE896"; // Download

    private static readonly Color GreenColor  = new Color { R = 108, G = 203, B = 95,  A = 255 };
    private static readonly Color YellowColor = new Color { R = 255, G = 192, B = 0,   A = 255 };
    private static readonly Color RedColor    = new Color { R = 255, G = 95,  B = 95,  A = 255 };
    private static OptionalColor Colored(Color c) =>
        new OptionalColor { HasValue = true, Color = c };

    public WifiPage(NetworkService service)
    {
        _service = service;
        Icon  = new IconInfo("\uE701");
        Title = "Network";
        Name  = "Network Details";
    }

    public override IListItem[] GetItems()
    {
        var info = _service.GetNetworkInfo();

        if (!info.Connected && !info.IsLimited)
        {
            AccentColor = Colored(RedColor);
            return [Row("Network", "Not connected", IconNoWifi, default, null)];
        }

        var rows = new List<IListItem>();

        // Network row carries the status tag (mirrors battery "Charge" pattern)
        var networkIcon  = info.IsWifi ? WifiIcon(info.SignalBars) : IconEthernet;
        var networkTitle = !string.IsNullOrEmpty(info.Ssid) ? info.Ssid : (info.IsWifi ? "Wi-Fi" : "Wired");
        var statusText   = info.IsLimited ? "Limited" : "Connected";
        var statusColor  = info.IsLimited ? Colored(YellowColor) : default;
        rows.Add(Row("Network", networkTitle, networkIcon, statusColor, statusText));

        if (info.IsWifi)
        {
            var (sigTag, sigColor) = SignalTag(info.SignalBars);
            rows.Add(Row("Signal", SignalText(info.SignalBars), WifiIcon(info.SignalBars), sigColor, sigTag));

            // Page accent reflects the weakest concern: limited > weak signal > good
            AccentColor = info.IsLimited        ? Colored(YellowColor)
                        : info.SignalBars <= 1  ? Colored(RedColor)
                        : info.SignalBars == 2  ? Colored(YellowColor)
                        : Colored(GreenColor);
        }
        else
        {
            AccentColor = info.IsLimited ? Colored(YellowColor) : Colored(GreenColor);
        }

        // Combined speed row \u2014 max adapter throughput in one line
        if (info.ReceiveMbps > 0 || info.TransmitMbps > 0)
        {
            var speedSubtitle = (info.ReceiveMbps > 0 && info.TransmitMbps > 0)
                ? $"\u2193 {info.ReceiveMbps:F0} / \u2191 {info.TransmitMbps:F0} Mbps"
                : info.ReceiveMbps > 0
                    ? $"\u2193 {info.ReceiveMbps:F0} Mbps"
                    : $"\u2191 {info.TransmitMbps:F0} Mbps";
            rows.Add(Row("Speed", speedSubtitle, IconDownload, default, null));
        }

        return [.. rows];
    }

    private static string WifiIcon(int bars) => bars switch
    {
        0 => IconNoWifi,
        1 => IconWifi1,
        2 => IconWifi2,
        3 => IconWifi3,
        _ => IconWifiFull,
    };

    private static string SignalText(int bars) => bars switch
    {
        0 => "Very weak (0/5)",
        1 => "Weak (1/5)",
        2 => "Fair (2/5)",
        3 => "Good (3/5)",
        4 => "Strong (4/5)",
        _ => "Excellent (5/5)",
    };

    private static (string tag, OptionalColor color) SignalTag(int bars) => bars switch
    {
        0 => ("None",      Colored(RedColor)),
        1 => ("Weak",      Colored(RedColor)),
        2 => ("Fair",      Colored(YellowColor)),
        3 => ("Good",      default),
        4 => ("Strong",    Colored(GreenColor)),
        _ => ("Excellent", Colored(GreenColor)),
    };

    private static ListItem Row(string title, string subtitle, string iconCode,
                                OptionalColor tagColor, string? tagText)
    {
        var item = new ListItem(new NoOpCommand())
        {
            Title    = title,
            Subtitle = subtitle,
            Icon     = new IconInfo(iconCode),
        };
        if (tagText is not null)
            item.Tags = [new Tag { Text = tagText, Foreground = tagColor }];
        return item;
    }
}