using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Notes.Models;
using NpuTools.Notes.Services;

namespace NpuTools.Notes.Pages;

internal sealed partial class FindRelatedNotesPage : ListPage
{
    private readonly NotesStore _store;
    private readonly NotesAiService _ai;
    private readonly NoteEntry _source;
    private int _started;

    public FindRelatedNotesPage(NotesStore store, NotesAiService ai, NoteEntry source)
    {
        _store = store;
        _ai = ai;
        _source = source;
        Id = "com.local.nputools.notes.findrelated";
        Title = "Find Related Notes";
        Name = "Find Related";
        Icon = NotesVisuals.Related;
        ShowDetails = true;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(LoadAsync);
        return [];
    }

    private async Task LoadAsync()
    {
        try
        {
            var all = _store.GetAll();
            var scored = new List<(NoteEntry Note, int Score)>(all.Count);
            foreach (var note in all)
            {
                if (string.Equals(note.FilePath, _source.FilePath, StringComparison.OrdinalIgnoreCase))
                    continue;
                int score = ScoreRelatedness(_source, note);
                if (score > 0)
                    scored.Add((note, score));
            }

            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
            var topCandidates = scored.Take(8).Select(s => s.Note).ToList();

            List<NoteEntry> related;
            if (topCandidates.Count == 0)
            {
                related = [];
            }
            else
            {
                var rerankInput = topCandidates.ConvertAll(n =>
                    (n.Id, n.Title, Snippet: string.IsNullOrWhiteSpace(n.Snippet) ? n.Title : n.Snippet));

                var rerankedIds = await _ai.RerankRelatedAsync(_source.Title, rerankInput);
                var idToNote = topCandidates.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
                related = [];
                foreach (string id in rerankedIds)
                {
                    if (idToNote.TryGetValue(id, out var note))
                        related.Add(note);
                }
            }

            IListItem[] items;
            if (related.Count == 0)
            {
                items =
                [
                    new ListItem(new NoOpCommand())
                    {
                        Title = "No related notes found",
                        Subtitle = "Try adding tags or shared keywords to improve matching",
                        Icon = NotesVisuals.Related,
                    },
                ];
            }
            else
            {
                items = new IListItem[related.Count];
                for (int i = 0; i < related.Count; i++)
                    items[i] = NoteItemFactory.Build(_store, _ai, related[i]);
            }

            IsLoading = false;
            RaiseItemsChanged(items.Length);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FindRelatedNotesPage.LoadAsync failed: {ex.GetType().Name}: {ex.Message}");
            IsLoading = false;
            RaiseItemsChanged(0);
        }
    }

    private static int ScoreRelatedness(NoteEntry source, NoteEntry candidate)
    {
        int score = 0;

        if (string.Equals(source.Category, candidate.Category, StringComparison.OrdinalIgnoreCase))
            score += 3;

        int sharedTags = source.Tags.Intersect(candidate.Tags, StringComparer.OrdinalIgnoreCase).Count();
        score += Math.Min(sharedTags * 2, 6);

        var sourceWords = GetSignificantWords(source.Title);
        var candidateWords = GetSignificantWords(candidate.Title);
        score += sourceWords.Intersect(candidateWords, StringComparer.OrdinalIgnoreCase).Count();

        return score;
    }

    private static HashSet<string> GetSignificantWords(string text) =>
        [.. text.Split([' ', '-', '_', '.', ','], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)];
}
