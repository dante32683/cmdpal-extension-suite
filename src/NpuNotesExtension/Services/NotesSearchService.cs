using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NpuTools.Notes.Models;

namespace NpuTools.Notes.Services;

internal sealed class NotesSearchService
{
    [SuppressMessage("Performance", "CA1822", Justification = "Service method uses instance call sites for provider injection consistency.")]
    public IReadOnlyList<NoteEntry> Search(IReadOnlyList<NoteEntry> entries, string query, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return entries
                .OrderByDescending(e => e.IsPinned)
                .ThenBy(e => e.PinOrder ?? int.MaxValue)
                .ThenByDescending(e => e.LastOpenedUtc ?? e.UpdatedUtc)
                .Take(maxResults)
                .ToList();
        }

        string trimmed = query.Trim();
        return entries
            .Select(e => (Entry: e, Score: Score(e, trimmed)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.LastOpenedUtc ?? x.Entry.UpdatedUtc)
            .Take(maxResults)
            .Select(x => x.Entry)
            .ToList();
    }

    internal static int Score(NoteEntry entry, string query)
    {
        int score = 0;
        if (entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 5;
        if (entry.Category.Contains(query, StringComparison.OrdinalIgnoreCase) || entry.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
            score += 3;
        if (entry.Body.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 2;
        if (score > 0 && IsWholeWord($"{entry.Title} {entry.Category} {string.Join(' ', entry.Tags)} {entry.Body}", query))
            score += 1;
        if (entry.IsPinned)
            score += 2;
        return score;
    }

    private static bool IsWholeWord(string text, string query)
    {
        int idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        while (idx >= 0)
        {
            bool left = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            int rightIndex = idx + query.Length;
            bool right = rightIndex >= text.Length || !char.IsLetterOrDigit(text[rightIndex]);
            if (left && right)
                return true;
            idx = text.IndexOf(query, idx + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
