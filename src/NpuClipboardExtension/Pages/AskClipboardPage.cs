using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Clipboard.Commands;
using NpuTools.Clipboard.Services;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Pages;

internal sealed partial class AskClipboardPage : DynamicListPage
{
    private readonly ClipboardStore _store;
    private readonly ClipboardSettingsStore _settings;
    private readonly ClipboardContentService _content;
    private readonly ClipboardAskService _ask;
    private IListItem[] _items;

    public AskClipboardPage(ClipboardStore store, ClipboardSettingsStore settings, ClipboardContentService content, ClipboardAskService ask)
    {
        _store = store;
        _settings = settings;
        _content = content;
        _ask = ask;
        Id = "com.local.nputools.clipboard.ask";
        Title = "Ask Clipboard";
        Name = "Ask";
        Icon = ClipboardVisuals.Search;
        PlaceholderText = "Ask for text, OCR, links, files...";
        _items = BuildItems(string.Empty);
        // Refresh when the store changes so live results reflect new copies, pins, deletes, and
        // keeper captures without forcing the user to retype the query.
        _store.Changed += OnStoreChanged;
    }

    private void OnStoreChanged() => RaiseItemsChanged();

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Type a natural-language clipboard query",
                    Subtitle = "Local search covers text, OCR text from images, links, emails, colors, and file paths.",
                    Icon = ClipboardVisuals.Search,
                },
            ];
        }

        var matches = _store.Search(null, query);
        var items = new List<IListItem>
        {
            new ListItem(new AskClipboardResultPage(_ask, query, matches))
            {
                Title = $"Ask Phi: {query}",
                Subtitle = "Summarize the most relevant local matches",
                Icon = ClipboardVisuals.Search,
                Tags = [ClipboardVisuals.StatusTag("ai")],
            },
        };

        foreach (var entry in matches)
        {
            items.Add(new ListItem(new CopyEntryCommand(_store, _settings, _content, entry.Id))
            {
                Title = entry.DisplayName,
                Subtitle = $"{entry.Kind} | {entry.CreatedAt:g}",
                Icon = ClipboardVisuals.Copy,
                Tags = [ClipboardVisuals.MutedTag("match")],
            });
            if (items.Count >= 12)
                break;
        }

        return [.. items];
    }
}

internal sealed partial class AskClipboardResultPage : ListPage
{
    private readonly ClipboardAskService _ask;
    private readonly string _query;
    private readonly IReadOnlyList<ClipboardEntry> _entries;
    private int _started;
    private string? _answer;

    public AskClipboardResultPage(ClipboardAskService ask, string query, IReadOnlyList<ClipboardEntry> entries)
    {
        _ask = ask;
        _query = query;
        _entries = entries;
        Id = "com.local.nputools.clipboard.ask.result";
        Title = "Ask Clipboard Result";
        Name = "Ask";
        Icon = ClipboardVisuals.Search;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunAsync);

        return _answer is null
            ?
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Searching clipboard history...",
                    Subtitle = "Using local history and Phi when available.",
                    Icon = ClipboardVisuals.Search,
                },
            ]
            :
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Answer",
                    Subtitle = _answer,
                    Icon = ClipboardVisuals.Search,
                },
            ];
    }

    private async Task RunAsync()
    {
        _answer = await _ask.AskAsync(_query, _entries).ConfigureAwait(false);
        IsLoading = false;
        RaiseItemsChanged();
    }
}
