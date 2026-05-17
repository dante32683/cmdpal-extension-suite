namespace NpuTools.Tests;

/// <summary>
/// Mirrors the prompt-building logic from TextRewriteService.BuildPrompt.
/// Kept in the test project so the test has no WinRT/MSIX dependency.
/// If BuildPrompt changes in production, update this mirror and the tests.
/// </summary>
internal enum RewriteMode
{
    FixGrammar,
    MakeFormal,
    MakeConcise,
    BulletPoints,
    Simplify,
    Custom,
}

internal static class TextRewritePromptHelper
{
    internal static string BuildPrompt(string text, RewriteMode mode, string? customInstruction = null)
    {
        string instruction = mode switch
        {
            RewriteMode.FixGrammar   => "Fix the grammar and spelling of the following text. Return only the corrected text with no explanation or commentary.",
            RewriteMode.MakeFormal   => "Rewrite the following text in a formal, professional tone. Return only the rewritten text with no explanation.",
            RewriteMode.MakeConcise  => "Make the following text more concise while preserving all key information. Return only the condensed text with no explanation.",
            RewriteMode.BulletPoints => "Convert the following text into clear, concise bullet points. Return only the bullet points with no explanation.",
            RewriteMode.Simplify     => "Simplify the following text so it is easy to understand. Return only the simplified text with no explanation.",
            RewriteMode.Custom       => string.IsNullOrWhiteSpace(customInstruction)
                                          ? "Rewrite the following text."
                                          : customInstruction.Trim(),
            _                        => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        return $"{instruction}\n\n{text}";
    }
}
