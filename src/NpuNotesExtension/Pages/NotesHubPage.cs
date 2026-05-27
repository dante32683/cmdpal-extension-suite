using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Commands;
using NpuTools.Notes.Services;

namespace NpuTools.Notes.Pages;

internal sealed partial class NotesHubPage : ListPage
{
    private readonly NotesStore _store;
    private readonly NotesSettingsStore _settings;
    private readonly NotesSearchService _search;
    private readonly NotesAiService _ai;

    public NotesHubPage(NotesStore store, NotesSettingsStore settings, NotesSearchService search, NotesAiService ai)
    {
        _store = store;
        _settings = settings;
        _search = search;
        _ai = ai;
        Id = "com.local.nputools.notes.hub";
        Title = "Notes";
        Name = "Open";
        Icon = NotesVisuals.Notes;
    }

    public override IListItem[] GetItems()
    {
        var settings = _settings.Current;
        var notes = _store.GetAll();
        var pinned = notes.Where(n => n.IsPinned).OrderBy(n => n.PinOrder ?? int.MaxValue).Take(settings.MaxRecentNotes).ToList();
        var recent = notes.Where(n => !n.IsPinned).OrderByDescending(n => n.LastOpenedUtc ?? n.UpdatedUtc).Take(settings.MaxRecentNotes).ToList();

        var items = new List<IListItem>
        {
            new ListItem(new CreateNotePage(_store, _settings))
            {
                Title = "Create Note",
                Subtitle = "Type or paste Markdown text",
                Icon = NotesVisuals.Add,
            },
            new ListItem(new SearchNotesPage(_store, _settings, _search, _ai))
            {
                Title = "Search Notes",
                Subtitle = "Search by title, content, category, or tags",
                Icon = NotesVisuals.Search,
            },
            new ListItem(new BrowseNotesPage(_store, _settings, _ai))
            {
                Title = "Browse Notes",
                Subtitle = "Open notes by category",
                Icon = NotesVisuals.Browse,
            },
            new ListItem(new OpenNotesFolderCommand(_settings))
            {
                Title = "Open Notes Folder",
                Subtitle = settings.NotesRoot,
                Icon = NotesVisuals.Folder,
            },
        };

        AddSection(items, "Pinned Notes", pinned);
        AddSection(items, "Recent Notes", recent);

        if (notes.Count == 0)
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = "No notes yet",
                Subtitle = "Create a note to start building your Markdown stack",
                Icon = NotesVisuals.Note,
            });
        }

        return [.. items];
    }

    private void AddSection(List<IListItem> items, string title, List<Models.NoteEntry> notes)
    {
        if (notes.Count == 0)
            return;

        items.Add(new ListItem(new NoOpCommand())
        {
            Title = title,
            Tags = [NotesVisuals.MutedTag("section")],
        });

        foreach (var note in notes)
            items.Add(NoteItemFactory.Build(_store, _ai, note));
    }
}
