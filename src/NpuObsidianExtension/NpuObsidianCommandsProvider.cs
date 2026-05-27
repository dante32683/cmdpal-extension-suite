using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Pages;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian;

internal sealed partial class NpuObsidianCommandsProvider : CommandProvider
{
    private readonly ObsidianSettingsStore _settingsStore = new();
    private readonly ObsidianMetadataStore _metadataStore;
    private readonly ObsidianVaultStore _vaultStore;
    private readonly ObsidianSearchService _search = new();
    private readonly ObsidianSettingsManager _settingsManager;
    private readonly ICommandItem[] _commands;

    public NpuObsidianCommandsProvider()
    {
        Id = "com.local.nputools.obsidian";
        DisplayName = "NPU Obsidian";
        Icon = ObsidianVisuals.Hub;

        _metadataStore = new ObsidianMetadataStore(_settingsStore);
        _vaultStore = new ObsidianVaultStore(_settingsStore, _metadataStore);
        _settingsManager = new ObsidianSettingsManager(_settingsStore);
        Settings = _settingsManager.Settings;

        _commands =
        [
            new CommandItem(new ObsidianHubPage(_vaultStore, _settingsStore, _search))
            {
                Title = "Obsidian",
                Subtitle = "Browse vault, pinned notes, and quick actions",
                Icon = ObsidianVisuals.Hub,
            },
            new CommandItem(new SearchObsidianNotesPage(_vaultStore, _settingsStore, _search))
            {
                Title = "Search Obsidian Notes",
                Subtitle = "Search vault by title, tags, headings, and content",
                Icon = ObsidianVisuals.Search,
            },
            new CommandItem(new CreateObsidianNotePage(_vaultStore, _settingsStore))
            {
                Title = "New Obsidian Note",
                Subtitle = "Create a Markdown note directly in the vault",
                Icon = ObsidianVisuals.Add,
            },
            new CommandItem(new OpenDailyNoteCommand(_settingsStore))
            {
                Title = "Open Daily Note",
                Subtitle = "Open today's daily note in Obsidian",
                Icon = ObsidianVisuals.Daily,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
