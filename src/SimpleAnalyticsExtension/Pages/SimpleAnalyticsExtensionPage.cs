using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

internal sealed partial class SimpleAnalyticsExtensionPage : ListPage
{
    private static readonly IconInfo BatteryIcon = BatteryDockPage.AddBandIcon;
    private static readonly IconInfo WifiIcon = WifiDockPage.AddBandIcon;
    private static readonly IconInfo CpuIcon = CpuDockPage.AddBandIcon;

    private readonly BatteryService _battery;
    private readonly NetworkService _network;
    private readonly CpuService _cpu;

    public SimpleAnalyticsExtensionPage(BatteryService battery, NetworkService network, CpuService cpu)
    {
        _battery = battery;
        _network = network;
        _cpu = cpu;

        Id = "com.dziad.simpleanalyticsextension.main";
        Icon = WifiIcon;
        Title = "Simple Analytics";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new BatteryPage(_battery))
            {
                Title = "Battery",
                Subtitle = "Battery details",
                Icon = BatteryIcon,
            },
            new ListItem(new WifiPage(_network))
            {
                Title = "Wi-Fi",
                Subtitle = "Network details",
                Icon = WifiIcon,
            },
            new ListItem(new NoOpCommand())
            {
                Title = "CPU",
                Subtitle = CpuSubtitle(),
                Icon = CpuIcon,
            },
        ];
    }

    private string CpuSubtitle()
    {
        try
        {
            return $"{_cpu.GetCpuPercent():F0}% usage";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Simple Analytics CPU summary failed: {ex}");
            return "Usage unavailable";
        }
    }
}
