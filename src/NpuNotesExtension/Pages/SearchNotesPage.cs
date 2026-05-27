using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Models;
using NpuTools.Notes.Services;

namespace NpuTools.Notes.Pages;

internal sealed partial class SearchNotesPage : DynamicListPage
{
    private readonly NotesStore _store;
    private readonly NotesSettingsStore _settings;
    private readonly NotesSearchService _search;
    private readonly NotesAiService _ai;
    private IListItem[] _items;
    private string _pendingSemanticQuery = string.Empty;

    public SearchNotesPage(NotesStore store, NotesSettingsStore settings, NotesSearchService search, NotesAiService ai)
    {
        _store = store;
        _settings = settings;
        _search = search;
        _ai = ai;
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
        _pendingSemanticQuery = string.Empty;

        var settings = _settings.Current;
        var notes = _store.GetAll();
        var results = _search.Search(notes, query, string.IsNullOrWhiteSpace(query) ? settings.MaxRecentNotes : settings.MaxSearchResults);

        if (!string.IsNullOrWhiteSpace(query) && results.Count < 3)
        {
            // Keyword results are sparse — kick off semantic fallback.
            _pendingSemanticQuery = query;
            var candidates = notes.Except(results, NoteEntryIdComparer.Instance).Take(50).ToList();
            _ = Task.Run(() => RunSemanticFallbackAsync(query, results, candidates));
        }

        if (results.Count == 0)
        {
            return
            [
                new ListItem(new CreateNotePage(_store, _settings))
                {
                    Title = string.IsNullOrWhiteSpace(query) ? "No notes yet" : $"No matches for \"{query}\"",
                    Subtitle = string.IsNullOrWhiteSpace(query)
                        ? "Create a note or change the search text"
                        : "Searching with AI...",
                    Icon = NotesVisuals.Search,
                    Tags = string.IsNullOrWhiteSpace(query) ? [] : [NotesVisuals.MutedTag("ai searching")],
                },
            ];
        }

        var items = new IListItem[results.Count];
        for (int i = 0; i < results.Count; i++)
            items[i] = NoteItemFactory.Build(_store, _ai, results[i]);
        return items;
    }

    private async Task RunSemanticFallbackAsync(string query, IReadOnlyList<NoteEntry> existing, IReadOnlyList<NoteEntry> candidates)
    {
        var aiResults = await _ai.SemanticSearchAsync(query, candidates);
        if (_pendingSemanticQuery != query || aiResults.Count == 0)
            return;

        var merged = new List<NoteEntry>(existing);
        var existingIds = new System.Collections.Generic.HashSet<string>(
            existing.Select(n => n.Id), System.StringComparer.OrdinalIgnoreCase);
        foreach (var n in aiResults)
        {
            if (existingIds.Add(n.Id))
                merged.Add(n);
        }

        _pendingSemanticQuery = string.Empty;
        _items = merged.Select(n => (IListItem)NoteItemFactory.Build(_store, _ai, n)).ToArray();
        RaiseItemsChanged(_items.Length);
    }

    private sealed class NoteEntryIdComparer : System.Collections.Generic.IEqualityComparer<NoteEntry>
    {
        public static readonly NoteEntryIdComparer Instance = new();
        public bool Equals(NoteEntry? x, NoteEntry? y) =>
            string.Equals(x?.Id, y?.Id, System.StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(NoteEntry obj) =>
            System.StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id);
    }
}
