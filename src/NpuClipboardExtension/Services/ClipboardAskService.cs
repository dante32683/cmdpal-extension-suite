using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using NpuTools.Clipboard.Data;

namespace NpuTools.Clipboard.Services;

internal sealed class ClipboardAskService
{
    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public async Task<string> AskAsync(string query, System.Collections.Generic.IReadOnlyList<ClipboardEntry> entries)
    {
        if (entries.Count == 0)
            return "No matching clipboard entries found.";

        try
        {
            if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
            {
                var ready = await LanguageModel.EnsureReadyAsync();
                if (ready.Status != AIFeatureReadyResultState.Success)
                    return BuildFallback(query, entries);
            }

            using var model = await LanguageModel.CreateAsync();
            string prompt = BuildPrompt(query, entries.Take(12).ToArray());
            var response = await model.GenerateResponseAsync(prompt);
            string text = (response.Text ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text) ? BuildFallback(query, entries) : text;
        }
        catch
        {
            return BuildFallback(query, entries);
        }
    }

    private static string BuildPrompt(string query, System.Collections.Generic.IReadOnlyList<ClipboardEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Find the most relevant clipboard history entries for the user's request. Be concise.");
        sb.AppendLine("Return a short answer naming the best entries and why they match.");
        sb.AppendLine();
        sb.AppendLine("Request:");
        sb.AppendLine(query);
        sb.AppendLine();
        sb.AppendLine("Entries:");
        foreach (var entry in entries)
        {
            string body = entry.Text ?? entry.OcrText ?? string.Join(", ", entry.FilePaths);
            if (body.Length > 600)
                body = body[..600];
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {entry.Id}: {entry.Kind} | {entry.DisplayName} | {body}");
        }
        return sb.ToString();
    }

    private static string BuildFallback(string query, System.Collections.Generic.IReadOnlyList<ClipboardEntry> entries)
    {
        var top = entries.Take(5).Select(e => $"{e.DisplayName} ({e.Kind})");
        return $"Top local matches for \"{query}\": {string.Join("; ", top)}.";
    }
}
