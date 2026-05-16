// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SimpleAnalyticsExtension;

public partial class SimpleAnalyticsExtensionCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settingsManager = new();
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _bands;

    public SimpleAnalyticsExtensionCommandsProvider()
    {
        Id          = "com.dziad.simpleanalyticsextension";
        DisplayName = "Simple Analytics";
        Icon        = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Settings    = _settingsManager.Settings;

        var battery = new BatteryService();
        var network = new NetworkService();
        var cpu     = new CpuService();

        _commands = [
            new CommandItem(new SimpleAnalyticsExtensionPage(_settingsManager)) { Title = DisplayName },
        ];

        _bands = [
            new CommandItem(new StatusDockPage(battery, network, cpu, _settingsManager)),
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => _bands;
}
