using ActionCenterExtension.Bands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ActionCenterExtension;

public partial class ActionCenterExtensionCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settingsManager = new();
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _dockBands;

    public ActionCenterExtensionCommandsProvider()
    {
        Id = "com.dziad.actioncenterextension";
        DisplayName = "Action Center";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Settings = _settingsManager.Settings;

        _commands = [
            new CommandItem(new ActionCenterExtensionPage(_settingsManager)) { Title = DisplayName },
        ];

        var quickSettings = new QuickSettingsCommand(_settingsManager);
        _dockBands = [new CommandItem(quickSettings) { Title = "Quick Settings", Icon = quickSettings.Icon }];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[]? GetDockBands() => _dockBands;
}
