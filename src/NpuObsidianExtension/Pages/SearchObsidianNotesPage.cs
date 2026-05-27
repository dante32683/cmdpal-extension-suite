using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

internal sealed partial class SearchObsidianNotesPage : DynamicListPage
{
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianIndexStore _indexStore;
    private readonly ObsidianSettingsStore _settings;
    private readonly ObsidianMetadataStore _metadata;
    private readonly ObsidianSearchService _search;
    private IListItem[] _items;

    public SearchObsidianNotesPage(
        ObsidianVaultStore store,
        ObsidianIndexStore indexStore,
        ObsidianSettingsStore settings,
        ObsidianMetadataStore metadata,
        ObsidianSearchService search)
    {
        _store = store;
        _indexStore = indexStore;
        _settings = settings;
        _metadata = metadata;
        _search = search;
        Id = "com.local.nputools.obsidian.search";
        Title = "Search Obsidian Notes";
        Name = "Search";
        Icon = ObsidianVisuals.Search;
        PlaceholderText = "Search vault...";
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
        if (!_store.IsVaultConfigured())
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Vault path not configured",
                    Subtitle = "Open settings and set the vault path first",
                    Icon = ObsidianVisuals.Warning,
                    Tags = [ObsidianVisuals.WarningTag("setup required")],
                },
            ];
        }

        var settings = _settings.Current;

        // Prefer the persistent index for rich body + backlink search; fall back to live scan.
        var notes = _indexStore.IsIndexed
            ? _indexStore.GetSearchableNotes(settings.VaultPath, _metadata)
            : _store.GetAll();

        int maxResults = string.IsNullOrWhiteSpace(query) ? settings.MaxRecentNotes : settings.MaxSearchResults;
        var results = _search.Search(notes, query, maxResults);

        if (results.Count == 0)
        {
            return
            [
                new ListItem(new CreateObsidianNotePage(_store, _settings))
                {
                    Title = string.IsNullOrWhiteSpace(query) ? "No notes in vault" : $"No matches for \"{query}\"",
                    Subtitle = _indexStore.IsIndexed
                        ? "Create a note or change the search text"
                        : "Index the vault for richer search, or create a note",
                    Icon = ObsidianVisuals.Search,
                },
            ];
        }

        var items = new IListItem[results.Count];
        for (int i = 0; i < results.Count; i++)
            items[i] = NoteItemFactory.Build(_store, _settings, results[i]);
        return items;
    }
}
