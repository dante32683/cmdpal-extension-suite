using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Pages;
using NpuTools.Notes.Services;

namespace NpuTools.Notes;

internal sealed partial class NpuNotesCommandsProvider : CommandProvider
{
    private readonly NotesSettingsStore _settingsStore = new();
    private readonly NotesIndexStore _indexStore;
    private readonly NotesStore _store;
    private readonly NotesSearchService _search = new();
    private readonly NotesSettingsManager _settingsManager;
    private readonly ICommandItem[] _commands;

    public NpuNotesCommandsProvider()
    {
        Id = "com.local.nputools.notes";
        DisplayName = "NPU Notes";
        Icon = NotesVisuals.Notes;

        _indexStore = new NotesIndexStore(_settingsStore);
        _store = new NotesStore(_settingsStore, _indexStore);
        _settingsManager = new NotesSettingsManager(_settingsStore);
        Settings = _settingsManager.Settings;

        _commands =
        [
            new CommandItem(new NotesHubPage(_store, _settingsStore, _search))
            {
                Title = "Notes",
                Subtitle = "Pinned and recent Markdown notes",
                Icon = NotesVisuals.Notes,
            },
            new CommandItem(new CreateNotePage(_store, _settingsStore))
            {
                Title = "Create Note",
                Subtitle = "Capture Markdown text into a note",
                Icon = NotesVisuals.Add,
            },
            new CommandItem(new SearchNotesPage(_store, _settingsStore, _search))
            {
                Title = "Search Notes",
                Subtitle = "Search note titles and content",
                Icon = NotesVisuals.Search,
            },
            new CommandItem(new BrowseNotesPage(_store, _settingsStore))
            {
                Title = "Browse Notes",
                Subtitle = "Browse notes by category",
                Icon = NotesVisuals.Browse,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
