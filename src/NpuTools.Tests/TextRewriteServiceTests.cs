using Xunit;

namespace NpuTools.Tests;

/// <summary>
/// Tests the prompt-building logic for TextRewriteService.
/// Uses TextRewritePromptHelper (a mirror of the production BuildPrompt) so that
/// the test has no WinRT / AI / MSIX dependency and runs on any Windows machine.
/// </summary>
public sealed class TextRewriteServiceTests
{
    // ── FixGrammar ─────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_FixGrammar_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt("Hello world.", RewriteMode.FixGrammar);
        Assert.Contains("Fix the grammar and spelling", prompt);
        Assert.Contains("Return only the corrected text with no explanation", prompt);
        Assert.Contains("Hello world.", prompt);
    }

    // ── MakeFormal ─────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_MakeFormal_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt("hey whats up", RewriteMode.MakeFormal);
        Assert.Contains("formal, professional tone", prompt);
        Assert.Contains("hey whats up", prompt);
    }

    // ── MakeConcise ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_MakeConcise_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt("some long text", RewriteMode.MakeConcise);
        Assert.Contains("more concise", prompt);
        Assert.Contains("some long text", prompt);
    }

    // ── BulletPoints ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_BulletPoints_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt("paragraph text", RewriteMode.BulletPoints);
        Assert.Contains("bullet points", prompt);
        Assert.Contains("paragraph text", prompt);
    }

    // ── Simplify ───────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_Simplify_ContainsCorrectInstruction()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt("complex prose", RewriteMode.Simplify);
        Assert.Contains("Simplify", prompt);
        Assert.Contains("complex prose", prompt);
    }

    // ── Custom ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_Custom_WithInstruction_UsesInstruction()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt(
            "some text",
            RewriteMode.Custom,
            "Translate to French");

        Assert.StartsWith("Translate to French", prompt);
        Assert.Contains("some text", prompt);
    }

    [Fact]
    public void BuildPrompt_Custom_NullInstruction_UsesDefaultFallback()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt("some text", RewriteMode.Custom, null);
        Assert.Contains("Rewrite the following text", prompt);
        Assert.Contains("some text", prompt);
    }

    [Fact]
    public void BuildPrompt_Custom_WhitespaceInstruction_UsesDefaultFallback()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt("some text", RewriteMode.Custom, "   ");
        Assert.Contains("Rewrite the following text", prompt);
    }

    // ── Format ─────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_AlwaysSeparatesInstructionAndTextWithDoubleNewline()
    {
        string prompt = TextRewritePromptHelper.BuildPrompt("body text", RewriteMode.FixGrammar);
        // Instruction and body are separated by exactly \n\n.
        int sep = prompt.IndexOf("\n\n", StringComparison.Ordinal);
        Assert.True(sep > 0, "Expected double newline separator between instruction and text.");
        Assert.Equal("body text", prompt[(sep + 2)..]);
    }

    [Fact]
    public void BuildPrompt_TextIsAppendedVerbatim()
    {
        string body = "This is the exact body text 123!";
        string prompt = TextRewritePromptHelper.BuildPrompt(body, RewriteMode.FixGrammar);
        Assert.EndsWith(body, prompt);
    }
}
