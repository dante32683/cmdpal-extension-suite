using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.DevToolbox.Commands;
using NpuTools.DevToolbox.Pages;
using NpuTools.DevToolbox.Services;

namespace NpuTools.DevToolbox;

internal sealed partial class NpuDevToolboxCommandsProvider : CommandProvider
{
    private readonly DevToolboxSettingsStore _settingsStore = new();
    private readonly RecentWorkspacesStore _recents = new();
    private readonly DevToolboxSettingsManager _settingsManager;
    private readonly ICommandItem[] _commands;

    public NpuDevToolboxCommandsProvider()
    {
        Id          = "com.local.nputools.devtoolbox";
        DisplayName = "NPU Dev Toolbox";
        Icon        = DevToolboxVisuals.Toolbox;

        _settingsManager = new DevToolboxSettingsManager(_settingsStore);
        Settings = _settingsManager.Settings;

        _commands =
        [
            new CommandItem(new DevToolboxHubPage(_settingsStore, _recents))
            {
                Title    = "Dev Toolbox",
                Subtitle = "Open workspaces in Explorer, Terminal, or IDE",
                Icon     = DevToolboxVisuals.Toolbox,
            },
            new CommandItem(new QuickOpenCommand("explorer", _settingsStore, _recents))
            {
                Title    = "Open in Explorer",
                Subtitle = "Pick a detected workspace and open it in File Explorer",
                Icon     = DevToolboxVisuals.Explorer,
            },
            new CommandItem(new QuickOpenCommand("terminal", _settingsStore, _recents))
            {
                Title    = "Open in Terminal",
                Subtitle = "Pick a detected workspace and open the configured terminal",
                Icon     = DevToolboxVisuals.Terminal,
            },
            new CommandItem(new QuickOpenCommand("ide", _settingsStore, _recents))
            {
                Title    = "Open in IDE",
                Subtitle = "Pick a detected workspace and open the configured IDE",
                Icon     = DevToolboxVisuals.Ide,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
