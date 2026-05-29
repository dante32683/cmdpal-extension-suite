using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Commands;
using NpuTools.Notes.Models;
using NpuTools.Notes.Services;
using static NpuTools.Notes.KeyChords;

namespace NpuTools.Notes.Pages;

internal sealed partial class NoteDetailPage : ListPage
{
    private readonly NotesStore _store;
    private readonly NotesAiService _ai;
    private readonly string _path;

    public NoteDetailPage(NotesStore store, NotesAiService ai, string path)
    {
        _store = store;
        _ai = ai;
        _path = path;
        Id = "com.local.nputools.notes.detail";
        Title = "Note";
        Name = "Open";
        Icon = NotesVisuals.Note;
        ShowDetails = true;
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

        _store.RecordOpened(entry);
        Title = entry.Title;

        return
        [
            new ListItem(new OpenNoteCommand(_store, entry.FilePath))
            {
                Title = "Open in Editor",
                Subtitle = entry.FilePath,
                Icon = NotesVisuals.Open,
                Details = NoteItemFactory.BuildDetails(entry),
                MoreCommands = NoteItemFactory.BuildMoreCommands(_store, _ai, entry),
            },
            new ListItem(new CopyTextCommand(entry.Body) { Name = "Copy Content" })
            {
                Title = "Copy Content",
                Subtitle = entry.Snippet,
                Icon = NotesVisuals.Copy,
                MoreCommands = [new CommandContextItem(new CopyTextCommand(entry.Body) { Name = "Copy Content", Icon = NotesVisuals.Copy }) { RequestedShortcut = CopyContent }],
            },
            new ListItem(new CopyTextCommand(entry.FilePath) { Name = "Copy Path" })
            {
                Title = "Copy Path",
                Subtitle = entry.FilePath,
                Icon = NotesVisuals.Copy,
                MoreCommands = [new CommandContextItem(new OpenNoteLocationCommand(entry.FilePath)) { RequestedShortcut = Reveal }],
            },
            new ListItem(new FindRelatedNotesPage(_store, _ai, entry))
            {
                Title = "Find Related Notes",
                Subtitle = "Find notes related to this one using AI",
                Icon = NotesVisuals.Related,
                Tags = [NotesVisuals.MutedTag("ai")],
            },
            new ListItem(new RenameNotePage(_store, entry))
            {
                Title = "Rename Note",
                Subtitle = "Change the title and filename of this note",
                Icon = NotesVisuals.Note,
                MoreCommands = [new CommandContextItem(new RenameNotePage(_store, entry)) { RequestedShortcut = Rename }],
            },
            new ListItem(new MoveNotePage(_store, entry))
            {
                Title = "Move Note",
                Subtitle = $"Current category: {entry.Category}",
                Icon = NotesVisuals.Folder,
                MoreCommands = [new CommandContextItem(new MoveNotePage(_store, entry)) { RequestedShortcut = Move }],
            },
            new ListItem(new TogglePinNoteCommand(_store, entry))
            {
                Title = entry.IsPinned ? "Unpin Note" : "Pin Note",
                Subtitle = entry.IsPinned ? "Remove from pinned notes" : "Keep this note at the top",
                Icon = NotesVisuals.Pin,
                Tags = entry.IsPinned ? [NotesVisuals.StatusTag("pinned")] : [],
                MoreCommands = [new CommandContextItem(new TogglePinNoteCommand(_store, entry)) { RequestedShortcut = Pin }],
            },
            new ListItem(new OpenNoteLocationCommand(entry.FilePath))
            {
                Title = "Reveal in Explorer",
                Subtitle = entry.FilePath,
                Icon = NotesVisuals.Folder,
                MoreCommands = [new CommandContextItem(new OpenNoteLocationCommand(entry.FilePath)) { RequestedShortcut = Reveal }],
            },
            new ListItem(new DeleteNotePage(_store, _ai, entry.FilePath))
            {
                Title = "Delete Note",
                Subtitle = "Move this note to the Recycle Bin",
                Icon = NotesVisuals.Delete,
                Tags = [NotesVisuals.CriticalTag("delete")],
                MoreCommands = [new CommandContextItem(new DeleteNotePage(_store, _ai, entry.FilePath)) { RequestedShortcut = Delete, IsCritical = true }],
            },
        ];
    }
}
