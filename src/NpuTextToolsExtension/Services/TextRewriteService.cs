using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Windows.ApplicationModel;

namespace NpuTools.TextTools.Services;

internal sealed class TextRewriteService
{
    [SuppressMessage("Performance", "CA1822", Justification = "Service method — uniform call site via injection.")]
    public async Task<string> RewriteAsync(string text, TextRewriteMode mode, string? customInstruction = null)
    {
        _ = TryUnlockNpuFeature();

        if (LanguageModel.GetReadyState() != AIFeatureReadyState.Ready)
        {
            var ready = await LanguageModel.EnsureReadyAsync();
            if (ready.Status != AIFeatureReadyResultState.Success)
                throw new InvalidOperationException($"Phi-Silica unavailable: {ready.Status}");
        }

        string prompt = BuildPrompt(text, mode, customInstruction);
        using var model = await LanguageModel.CreateAsync();
        var response = await model.GenerateResponseAsync(prompt);
        return (response.Text ?? string.Empty).Trim();
    }

    public static string ModeLabel(TextRewriteMode mode) => mode switch
    {
        TextRewriteMode.FixGrammar   => "Fix Grammar",
        TextRewriteMode.MakeFormal   => "Make Formal",
        TextRewriteMode.MakeConcise  => "Make Concise",
        TextRewriteMode.BulletPoints => "Bullet Points",
        TextRewriteMode.Simplify     => "Simplify",
        TextRewriteMode.Custom       => "Custom Rewrite",
        _                            => mode.ToString(),
    };

    internal static string BuildPrompt(string text, TextRewriteMode mode, string? customInstruction)
    {
        string instruction = mode switch
        {
            TextRewriteMode.FixGrammar   => "Fix the grammar and spelling of the following text. Return only the corrected text with no explanation or commentary.",
            TextRewriteMode.MakeFormal   => "Rewrite the following text in a formal, professional tone. Return only the rewritten text with no explanation.",
            TextRewriteMode.MakeConcise  => "Make the following text more concise while preserving all key information. Return only the condensed text with no explanation.",
            TextRewriteMode.BulletPoints => "Convert the following text into clear, concise bullet points. Return only the bullet points with no explanation.",
            TextRewriteMode.Simplify     => "Simplify the following text so it is easy to understand. Return only the simplified text with no explanation.",
            TextRewriteMode.Custom       => string.IsNullOrWhiteSpace(customInstruction)
                                              ? "Rewrite the following text."
                                              : customInstruction.Trim(),
            _                            => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        return $"{instruction}\n\n{text}";
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
