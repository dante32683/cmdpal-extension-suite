using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using NpuTools.Obsidian.Models;
using Windows.ApplicationModel;

namespace NpuTools.Obsidian.Services;

internal sealed partial class ObsidianAiService
{
    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public async Task<string> SummarizeAsync(string title, string body)
    {
        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
        {
            var ready = await LanguageModel.EnsureReadyAsync();
            if (ready.Status != AIFeatureReadyResultState.Success)
                throw new InvalidOperationException($"Phi-Silica unavailable: {ready.Status}");
        }

        // Cap body to avoid extremely long prompts.
        string bodySnippet = body.Length > 3000 ? body[..3000] + "..." : body;
        string prompt = $"""
            Summarize the following note in 1-2 sentences. Return only the summary text, no preamble or explanation.

            Title: {title}

            {bodySnippet}
            """;

        using var model = await LanguageModel.CreateAsync();
        var response = await model.GenerateResponseAsync(prompt);
        return (response.Text ?? string.Empty).Trim();
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public async Task<SmartCaptureProposal> SmartCaptureAsync(string roughText, IReadOnlyList<string> existingFolders)
    {
        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
        {
            var ready = await LanguageModel.EnsureReadyAsync();
            if (ready.Status != AIFeatureReadyResultState.Success)
                return FallbackProposal(roughText);
        }

        string folderList = existingFolders.Count > 0
            ? string.Join(", ", existingFolders)
            : "misc";

        string prompt = $$"""
            You are a note-taking assistant. Given rough text, propose a structured note.
            Available folders: {{folderList}}

            Respond ONLY with valid JSON in this exact format (no markdown, no extra text):
            {"title":"...","folder":"...","tags":["..."],"body":"..."}

            Rules:
            - title: 3-8 words, descriptive, Title Case
            - folder: pick the most relevant from the available folders, or "misc"
            - tags: 1-3 lowercase tags
            - body: clean up the rough text into well-formatted Markdown

            Rough text:
            {{roughText}}
            """;

        try
        {
            using var model = await LanguageModel.CreateAsync();
            var response = await model.GenerateResponseAsync(prompt);
            string json = (response.Text ?? "").Trim();

            // Strip markdown code fence if model wraps the JSON
            if (json.StartsWith("```", StringComparison.Ordinal))
            {
                int first = json.IndexOf('\n') + 1;
                int last = json.LastIndexOf("```", StringComparison.Ordinal);
                if (first > 0 && last > first)
                    json = json[first..last].Trim();
            }

            var proposal = JsonSerializer.Deserialize(json, SmartCaptureJsonContext.Default.SmartCaptureProposal);
            if (proposal is not null && !string.IsNullOrWhiteSpace(proposal.Title))
                return proposal;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianAiService.SmartCaptureAsync failed: {ex.GetType().Name}: {ex.Message}");
        }

        return FallbackProposal(roughText);
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform instance call sites.")]
    public async Task<List<string>> RerankRelatedAsync(
        string targetTitle,
        string targetSummary,
        List<(string RelativePath, string Title, string Snippet)> candidates)
    {
        if (candidates.Count == 0)
            return [];

        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
            return candidates.ConvertAll(c => c.RelativePath);

        try
        {
            var candidateLines = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
                candidateLines.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"{i + 1}. \"{candidates[i].Title}\" — {candidates[i].Snippet}");

            string prompt = $"""
                I have a note titled "{targetTitle}".
                Summary: {(string.IsNullOrEmpty(targetSummary) ? "(no summary)" : targetSummary)}

                Rank these candidate related notes by relevance (most relevant first).
                Return ONLY a comma-separated list of numbers (e.g. "3,1,2"), nothing else.

                Candidates:
                {candidateLines}
                """;

            using var model = await LanguageModel.CreateAsync();
            var response = await model.GenerateResponseAsync(prompt);
            string raw = (response.Text ?? "").Trim();

            // Parse "3,1,2" → reordered list
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = new List<string>(candidates.Count);
            var seen = new HashSet<int>();
            foreach (string part in parts)
            {
                if (int.TryParse(part, out int idx) && idx >= 1 && idx <= candidates.Count && seen.Add(idx - 1))
                    result.Add(candidates[idx - 1].RelativePath);
            }

            // Append any candidates not mentioned by Phi
            for (int i = 0; i < candidates.Count; i++)
            {
                if (seen.Add(i))
                    result.Add(candidates[i].RelativePath);
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ObsidianAiService.RerankRelatedAsync failed: {ex.GetType().Name}: {ex.Message}");
            return candidates.ConvertAll(c => c.RelativePath);
        }
    }

    private static SmartCaptureProposal FallbackProposal(string roughText)
    {
        string firstLine = roughText.Trim().Split('\n')[0].Trim();
        string title = firstLine.Length > 80 ? firstLine[..80].Trim() : firstLine;
        if (string.IsNullOrWhiteSpace(title))
            title = "Untitled Note";

        return new SmartCaptureProposal
        {
            Title = title,
            Folder = "misc",
            Tags = [],
            Body = roughText.Trim(),
        };
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

    [JsonSerializable(typeof(SmartCaptureProposal))]
    [JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
    private sealed partial class SmartCaptureJsonContext : JsonSerializerContext { }
}
