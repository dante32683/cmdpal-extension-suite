using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Commands;
using NpuTools.Notes.Services;
using static NpuTools.Notes.KeyChords;

namespace NpuTools.Notes.Pages;

internal sealed partial class DeleteNotePage : ListPage
{
    private readonly NotesStore _store;
    private readonly NotesAiService _ai;
    private readonly string _path;

    public DeleteNotePage(NotesStore store, NotesAiService ai, string path)
    {
        _store = store;
        _ai = ai;
        _path = path;
        Id = "com.local.nputools.notes.delete";
        Title = "Delete Note";
        Name = "Delete";
        Icon = NotesVisuals.Delete;
    }

    public override IListItem[] GetItems()
    {
        var entry = _store.GetByPath(_path);
        if (entry is null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Note no longer exists",
                    Subtitle = _path,
                    Icon = NotesVisuals.Note,
                },
            ];
        }

        return
        [
            new ListItem(new NoteDetailPage(_store, _ai, entry.FilePath))
            {
                Title = "Cancel",
                Subtitle = "Return to the note",
                Icon = NotesVisuals.Note,
            },
            new ListItem(new DeleteNoteCommand(_store, entry.FilePath))
            {
                Title = "Delete Note",
                Subtitle = $"Move \"{entry.Title}\" to the Recycle Bin",
                Icon = NotesVisuals.Delete,
                Tags = [NotesVisuals.CriticalTag("destructive")],
                MoreCommands = [new CommandContextItem(new DeleteNoteCommand(_store, entry.FilePath)) { RequestedShortcut = Delete, IsCritical = true }],
            },
        ];
    }
}
