using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NpuTools.Obsidian.Models;

namespace NpuTools.Obsidian.Services;

internal sealed class ObsidianSearchService
{
    [SuppressMessage("Performance", "CA1822", Justification = "Service method — instance call sites for provider injection consistency.")]
    public IReadOnlyList<ObsidianNote> Search(IReadOnlyList<ObsidianNote> notes, string query, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return notes
                .OrderByDescending(n => n.IsPinned)
                .ThenBy(n => n.PinOrder ?? int.MaxValue)
                .ThenByDescending(n => n.LastOpenedUtc ?? n.LastModifiedUtc)
                .Take(maxResults)
                .ToList();
        }

        string trimmed = query.Trim();
        return notes
            .Select(n => (Note: n, Score: Score(n, trimmed)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Note.LastOpenedUtc ?? x.Note.LastModifiedUtc)
            .Take(maxResults)
            .Select(x => x.Note)
            .ToList();
    }

    internal static int Score(ObsidianNote note, string query)
    {
        int score = 0;

        if (note.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 10;

        if (note.Aliases.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase)))
            score += 8;

        if (note.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
            score += 6;

        if (note.Headings.Any(h => h.Contains(query, StringComparison.OrdinalIgnoreCase)))
            score += 4;

        if (note.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 3;

        if (note.Body.Contains(query, StringComparison.OrdinalIgnoreCase))
            score += 2;

        if (score > 0 && IsWholeWord(note.Title + " " + string.Join(' ', note.Tags) + " " + string.Join(' ', note.Aliases), query))
            score += 2;

        if (note.IsPinned)
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
