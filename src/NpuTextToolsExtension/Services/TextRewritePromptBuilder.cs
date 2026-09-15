using System;

namespace NpuTools.TextTools.Services;

internal static class TextRewritePromptBuilder
{
    internal static string Build(string text, TextRewriteMode mode, string? customInstruction = null)
    {
        string instruction = mode switch
        {
            TextRewriteMode.FixGrammar => "Fix the grammar and spelling of the following text. Return only the corrected text with no explanation or commentary.",
            TextRewriteMode.MakeFormal => "Rewrite the following text in a formal, professional tone. Return only the rewritten text with no explanation.",
            TextRewriteMode.MakeConcise => "Make the following text more concise while preserving all key information. Return only the condensed text with no explanation.",
            TextRewriteMode.BulletPoints => "Convert the following text into clear, concise bullet points. Return only the bullet points with no explanation.",
            TextRewriteMode.Simplify => "Simplify the following text so it is easy to understand. Return only the simplified text with no explanation.",
            TextRewriteMode.Custom => string.IsNullOrWhiteSpace(customInstruction)
                ? "Rewrite the following text."
                : customInstruction.Trim(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        return $"{instruction}\n\n{text}";
    }
}
