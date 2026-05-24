using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Services;

namespace NpuTools.Notes.Pages;

internal sealed partial class SearchNotesPage : DynamicListPage
{
    private readonly NotesStore _store;
    private readonly NotesSettingsStore _settings;
    private readonly NotesSearchService _search;
    private IListItem[] _items;

    public SearchNotesPage(NotesStore store, NotesSettingsStore settings, NotesSearchService search)
    {
        _store = store;
        _settings = settings;
        _search = search;
        Id = "com.local.nputools.notes.search";
        Title = "Search Notes";
        Name = "Search";
        Icon = NotesVisuals.Search;
        PlaceholderText = "Search notes...";
        ShowDetails = true;
        _items = BuildItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems()
    {
        _items = BuildItems(SearchText?.Trim() ?? string.Empty);
        return _items;
    }

    private IListItem[] BuildItems(string query)
    {
        var settings = _settings.Current;
        var notes = _store.GetAll();
        var results = _search.Search(notes, query, string.IsNullOrWhiteSpace(query) ? settings.MaxRecentNotes : settings.MaxSearchResults);

        if (results.Count == 0)
        {
            return
            [
                new ListItem(new CreateNotePage(_store, _settings))
                {
                    Title = string.IsNullOrWhiteSpace(query) ? "No notes yet" : $"No matches for \"{query}\"",
                    Subtitle = "Create a note or change the search text",
                    Icon = NotesVisuals.Search,
                },
            ];
        }

        var items = new IListItem[results.Count];
        for (int i = 0; i < results.Count; i++)
            items[i] = NoteItemFactory.Build(_store, results[i]);
        return items;
    }
}
