using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Commands;
using NpuTools.Obsidian.Services;
using static NpuTools.Obsidian.KeyChords;

namespace NpuTools.Obsidian.Pages;

internal sealed partial class DeleteObsidianNotePage : ListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianIndexStore _indexStore;
    private readonly ObsidianSettingsStore _settings;
    private readonly ObsidianAiService _ai;
    private readonly string _path;

    public DeleteObsidianNotePage(
        ObsidianVaultStore store,
        ObsidianIndexStore indexStore,
        ObsidianSettingsStore settings,
        ObsidianAiService ai,
        string path)
    {
        _store = store;
        _indexStore = indexStore;
        _settings = settings;
        _ai = ai;
        _path = path;
        Id = "com.local.nputools.obsidian.delete";
        Title = "Delete Note";
        Name = "Delete";
        Icon = ObsidianVisuals.Delete;
    }

    public override IListItem[] GetItems()
    {
        var note = _store.GetByPath(_path);
        if (note is null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Note no longer exists",
                    Subtitle = _path,
                    Icon = ObsidianVisuals.Warning,
                },
            ];
        }

        return
        [
            new ListItem(new NotePreviewPage(_store, _indexStore, _settings, _ai, note.AbsolutePath))
            {
                Title = "Cancel",
                Subtitle = "Return to the note",
                Icon = ObsidianVisuals.Note,
            },
            new ListItem(new DeleteObsidianNoteCommand(_store, _indexStore, note.AbsolutePath))
            {
                Title = "Delete Note",
                Subtitle = $"Move \"{note.Title}\" to the Recycle Bin",
                Icon = ObsidianVisuals.Delete,
                Tags = [ObsidianVisuals.CriticalTag("destructive")],
                MoreCommands = [new CommandContextItem(new DeleteObsidianNoteCommand(_store, _indexStore, note.AbsolutePath)) { RequestedShortcut = Delete, IsCritical = true }],
            },
        ];
    }
}
