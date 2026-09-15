using Xunit;
using NpuTools.TextTools;
using NpuTools.TextTools.Services;

namespace NpuTools.Tests;

/// <summary>
/// Tests the prompt-building logic for TextRewriteService.
/// Exercises the production prompt builder directly without loading WinRT AI APIs.
/// </summary>
public sealed class TextRewriteServiceTests
{
    // ── FixGrammar ─────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_FixGrammar_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptBuilder.Build("Hello world.", TextRewriteMode.FixGrammar);
        Assert.Contains("Fix the grammar and spelling", prompt);
        Assert.Contains("Return only the corrected text with no explanation", prompt);
        Assert.Contains("Hello world.", prompt);
    }

    // ── MakeFormal ─────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_MakeFormal_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptBuilder.Build("hey whats up", TextRewriteMode.MakeFormal);
        Assert.Contains("formal, professional tone", prompt);
        Assert.Contains("hey whats up", prompt);
    }

    // ── MakeConcise ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_MakeConcise_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptBuilder.Build("some long text", TextRewriteMode.MakeConcise);
        Assert.Contains("more concise", prompt);
        Assert.Contains("some long text", prompt);
    }

    // ── BulletPoints ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_BulletPoints_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptBuilder.Build("paragraph text", TextRewriteMode.BulletPoints);
        Assert.Contains("bullet points", prompt);
        Assert.Contains("paragraph text", prompt);
    }

    // ── Simplify ───────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_Simplify_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptBuilder.Build("complex prose", TextRewriteMode.Simplify);
        Assert.Contains("Simplify", prompt);
        Assert.Contains("complex prose", prompt);
    }

    // ── Custom ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_Custom_WithInstruction_UsesInstruction()
    {
        string prompt = TextRewritePromptBuilder.Build(
            "some text",
            TextRewriteMode.Custom,
            "Translate to French");

        Assert.StartsWith("Translate to French", prompt);
        Assert.Contains("some text", prompt);
    }

    [Fact]
    public void BuildPrompt_Custom_NullInstruction_UsesDefaultFallback()
    {
        string prompt = TextRewritePromptBuilder.Build("some text", TextRewriteMode.Custom);
        Assert.Contains("Rewrite the following text", prompt);
        Assert.Contains("some text", prompt);
    }

    [Fact]
    public void BuildPrompt_Custom_WhitespaceInstruction_UsesDefaultFallback()
    {
        string prompt = TextRewritePromptBuilder.Build("some text", TextRewriteMode.Custom, "   ");
        Assert.Contains("Rewrite the following text", prompt);
    }

    // ── Format ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_AlwaysSeparatesInstructionAndTextWithDoubleNewline()
    {
        string prompt = TextRewritePromptBuilder.Build("body text", TextRewriteMode.FixGrammar);
        // Instruction and body are separated by exactly \n\n.
        int sep = prompt.IndexOf("\n\n", StringComparison.Ordinal);
        Assert.True(sep > 0, "Expected double newline separator between instruction and text.");
        Assert.Equal("body text", prompt[(sep + 2)..]);
    }

    [Fact]
    public void BuildPrompt_TextIsAppendedVerbatim()
    {
        string body = "This is the exact body text 123!";
        string prompt = TextRewritePromptBuilder.Build(body, TextRewriteMode.FixGrammar);
        Assert.EndsWith(body, prompt);
    }
}
