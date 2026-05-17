using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

public partial class SimpleAnalyticsExtensionCommandsProvider : CommandProvider
{
    private static readonly IconInfo ProviderIcon = new("\uE701");
    private readonly SettingsManager _settingsManager = new();
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _bands;

    public SimpleAnalyticsExtensionCommandsProvider()
    {
        Id          = "com.dziad.simpleanalyticsextension";
        DisplayName = "Simple Analytics";
        Icon        = ProviderIcon;
        Settings    = _settingsManager.Settings;

        var battery = new BatteryService();
        var network = new NetworkService();
        var cpu     = new CpuService();

        _commands = [
            new CommandItem(new SimpleAnalyticsExtensionPage(_settingsManager)) { Title = DisplayName, Icon = Icon },
        ];

        _bands = [
            new CommandItem(new StatusDockPage(battery, network, cpu, _settingsManager)) { Title = "Status", Icon = Icon },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => _bands;
}
