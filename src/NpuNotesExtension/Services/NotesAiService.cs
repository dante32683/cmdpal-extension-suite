using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using NpuTools.Notes.Models;
using Windows.ApplicationModel;

namespace NpuTools.Notes.Services;

internal sealed partial class NotesAiService
{
    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public async Task<(string Title, string Body)> CleanupNoteAsync(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(body) && string.Equals(title, "Untitled Note", StringComparison.Ordinal))
            return (title, body);

        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
            return (title, body);

        try
        {
            string prompt = $"""
                Clean up this quick note for grammar and readability.
                Also suggest a concise title (10 words or fewer) capturing the main idea.
                Current title: {title}
                Note body:
                {body}

                Respond with exactly two sections and no other text:
                TITLE: <your title>
                BODY:
                <cleaned body>
                """;

            using var model = await LanguageModel.CreateAsync();
            var response = await model.GenerateResponseAsync(prompt);
            string raw = (response.Text ?? string.Empty).Trim();

            string cleanTitle = title;
            string cleanBody = body;
            string[] lines = raw.Split('\n');
            int bodyStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
                    cleanTitle = line["TITLE:".Length..].Trim();
                else if (line.StartsWith("BODY:", StringComparison.OrdinalIgnoreCase))
                {
                    bodyStart = i + 1;
                    break;
                }
            }

            if (bodyStart >= 0 && bodyStart < lines.Length)
                cleanBody = string.Join('\n', lines[bodyStart..]).Trim();

            if (string.IsNullOrWhiteSpace(cleanTitle))
                cleanTitle = title;

            return (cleanTitle, cleanBody);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesAiService.CleanupNoteAsync failed: {ex.GetType().Name}: {ex.Message}");
            return (title, body);
        }
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public async Task<List<string>> RerankRelatedAsync(
        string targetTitle,
        List<(string Id, string Title, string Snippet)> candidates)
    {
        if (candidates.Count == 0)
            return [];

        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
            return candidates.ConvertAll(c => c.Id);

        try
        {
            var candidateLines = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
                candidateLines.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{i + 1}. \"{candidates[i].Title}\" -- {candidates[i].Snippet}");

            string prompt = $"""
                I have a note titled "{targetTitle}".

                Rank these candidate related notes by relevance (most relevant first).
                Return ONLY a comma-separated list of numbers (e.g. "3,1,2"), nothing else.

                Candidates:
                {candidateLines}
                """;

            using var model = await LanguageModel.CreateAsync();
            var response = await model.GenerateResponseAsync(prompt);
            string raw = (response.Text ?? string.Empty).Trim();

            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = new List<string>(candidates.Count);
            var seen = new HashSet<int>();
            foreach (string part in parts)
            {
                if (int.TryParse(part, out int idx) && idx >= 1 && idx <= candidates.Count && seen.Add(idx - 1))
                    result.Add(candidates[idx - 1].Id);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (seen.Add(i))
                    result.Add(candidates[i].Id);
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesAiService.RerankRelatedAsync failed: {ex.GetType().Name}: {ex.Message}");
            return candidates.ConvertAll(c => c.Id);
        }
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public async Task<List<NoteEntry>> SemanticSearchAsync(string query, IReadOnlyList<NoteEntry> candidates)
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(query))
            return [];

        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
            return [];

        try
        {
            var candidateLines = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
            {
                string snippet = string.IsNullOrWhiteSpace(candidates[i].Snippet) ? candidates[i].Title : candidates[i].Snippet;
                candidateLines.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{i + 1}. \"{candidates[i].Title}\" -- {snippet}");
            }

            string prompt = $"""
                I am searching my notes for: "{query}"

                Which of these notes are relevant? List the numbers of relevant notes in order of relevance.
                Return ONLY a comma-separated list of numbers (e.g. "3,1,2"), nothing else.
                If none are relevant, return an empty string.

                Notes:
                {candidateLines}
                """;

            using var model = await LanguageModel.CreateAsync();
            var response = await model.GenerateResponseAsync(prompt);
            string raw = (response.Text ?? string.Empty).Trim();

            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = new List<NoteEntry>(parts.Length);
            var seen = new HashSet<int>();
            foreach (string part in parts)
            {
                if (int.TryParse(part, out int idx) && idx >= 1 && idx <= candidates.Count && seen.Add(idx - 1))
                    result.Add(candidates[idx - 1]);
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NotesAiService.SemanticSearchAsync failed: {ex.GetType().Name}: {ex.Message}");
            return [];
        }
    }

    private static bool TryUnlockNpuFeature()
    {
        try
        {
            const string featureId = "com.microsoft.windows.ai.languagemodel";
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel\LimitedAccessFeatures\{featureId}");
            string? lafKey = key?.GetValue(string.Empty)?.ToString();
            if (string.IsNullOrEmpty(lafKey)) return false;
            string pfn = Package.Current.Id.FamilyName;
            string input = $"{featureId}!{lafKey}!{pfn}";
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            string token = Convert.ToBase64String(hashBytes[..16]);
            string publisherId = pfn.Split('_')[1];
            string attestation = $"{publisherId} has registered their use of {featureId} with Microsoft and agrees to the terms of use.";
            var result = LimitedAccessFeatures.TryUnlockFeature(featureId, token, attestation);
            return result.Status is LimitedAccessFeatureStatus.Available or LimitedAccessFeatureStatus.AvailableWithoutToken;
        }
        catch
        {
            return false;
        }
    }
}
