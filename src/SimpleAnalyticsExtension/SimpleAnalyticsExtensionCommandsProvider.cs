using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

public partial class SimpleAnalyticsExtensionCommandsProvider : CommandProvider
{
    private static readonly IconInfo ProviderIcon = new("\uE701");
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _bands;

    public SimpleAnalyticsExtensionCommandsProvider()
    {
        Id          = "com.dziad.simpleanalyticsextension";
        DisplayName = "Simple Analytics";
        Icon        = ProviderIcon;

        var battery = new BatteryService();
        var network = new NetworkService();
        var cpu     = new CpuService();

        _commands = [
            new CommandItem(new SimpleAnalyticsExtensionPage(battery, network, cpu)) { Title = DisplayName, Subtitle = "Battery, Wi-Fi, and CPU dock analytics", Icon = Icon },
        ];

        _bands = [
            new CommandItem(new BatteryDockPage(battery)) { Title = "Battery", Icon = BatteryDockPage.AddBandIcon },
            new CommandItem(new WifiDockPage(network)) { Title = "Wi-Fi", Icon = WifiDockPage.AddBandIcon },
            new CommandItem(new CpuDockPage(cpu)) { Title = "CPU", Icon = CpuDockPage.AddBandIcon },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => _bands;
}
