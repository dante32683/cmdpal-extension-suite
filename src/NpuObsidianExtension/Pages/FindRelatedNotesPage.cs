using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;

namespace NpuTools.Obsidian.Pages;

// Async page: scores candidates deterministically, then Phi-reranks the top 8.
internal sealed partial class FindRelatedNotesPage : ListPage
{
    private readonly ObsidianNote _target;
    private readonly ObsidianVaultStore _store;
    private readonly ObsidianIndexStore _indexStore;
    private readonly ObsidianSettingsStore _settings;
    private readonly ObsidianAiService _ai;
    private int _started;
    private IListItem[]? _results;
    private string? _errorMessage;

    public FindRelatedNotesPage(
        ObsidianNote target,
        ObsidianVaultStore store,
        ObsidianIndexStore indexStore,
        ObsidianSettingsStore settings,
        ObsidianAiService ai)
    {
        _target = target;
        _store = store;
        _indexStore = indexStore;
        _settings = settings;
        _ai = ai;
        Id = "com.local.nputools.obsidian.related";
        Title = $"Related: {target.Title}";
        Name = "Find Related";
        Icon = ObsidianVisuals.Related;
        IsLoading = true;
    }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunAsync);

        if (_errorMessage is not null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Could not find related notes",
                    Subtitle = _errorMessage,
                    Icon = ObsidianVisuals.Warning,
                    Tags = [ObsidianVisuals.WarningTag("error")],
                },
            ];
        }

        if (_results is null)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Finding related notes…",
                    Subtitle = _target.Title,
                    Icon = ObsidianVisuals.Related,
                },
            ];
        }

        return _results;
    }

    private async Task RunAsync()
    {
        try
        {
            var settings = _settings.Current;
            IReadOnlyList<ObsidianNote> allNotes = _indexStore.IsIndexed
                ? _indexStore.GetSearchableNotes(settings.VaultPath)
                : _store.GetAll();

            var scored = new List<(ObsidianNote Note, int Score)>();
            string targetFolder = Path.GetDirectoryName(_target.RelativePath) ?? "";

            foreach (var note in allNotes)
            {
                if (string.Equals(note.AbsolutePath, _target.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                int score = 0;

                string folder = Path.GetDirectoryName(note.RelativePath) ?? "";
                if (!string.IsNullOrEmpty(targetFolder) && string.Equals(folder, targetFolder, StringComparison.OrdinalIgnoreCase))
                    score += 3;

                // Shared tags, capped at +6
                int sharedTags = _target.Tags.Count(t => note.Tags.Contains(t, StringComparer.OrdinalIgnoreCase));
                score += Math.Min(sharedTags * 2, 6);

                // Mutual backlink bonus
                if (note.Backlinks.Contains(_target.RelativePath, StringComparer.OrdinalIgnoreCase))
                    score += 4;
                if (_target.Backlinks.Contains(note.RelativePath, StringComparer.OrdinalIgnoreCase))
                    score += 4;

                // Title keyword overlap
                var words = _target.Title.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
                score += words.Count(w => w.Length > 3 && note.Title.Contains(w, StringComparison.OrdinalIgnoreCase));

                if (score > 0)
                    scored.Add((note, score));
            }

            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
            var top = scored.Take(8).ToList();

            if (top.Count == 0)
            {
                _results =
                [
                    new ListItem(new NoOpCommand())
                    {
                        Title = "No related notes found",
                        Subtitle = "Index the vault for richer relatedness scoring",
                        Icon = ObsidianVisuals.Note,
                        Tags = [ObsidianVisuals.MutedTag("try indexing")],
                    },
                ];
                return;
            }

            var rerankInput = top.Select(c => (c.Note.RelativePath, c.Note.Title, c.Note.Snippet)).ToList();
            var rerankedPaths = await _ai.RerankRelatedAsync(_target.Title, _target.AiSummary, rerankInput);

            var byPath = top.ToDictionary(c => c.Note.RelativePath, c => c.Note, StringComparer.OrdinalIgnoreCase);
            var items = new List<IListItem>(rerankedPaths.Count);
            foreach (string path in rerankedPaths)
            {
                if (byPath.TryGetValue(path, out var note))
                    items.Add(NoteItemFactory.Build(_store, _indexStore, _settings, _ai, note));
            }

            _results = [.. items];
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            RaiseItemsChanged();
        }
    }
}
