using System;
using System.IO;
using NpuTools.Obsidian.Services;
using Xunit;

namespace NpuTools.Tests;

public sealed class ObsidianParserTests
{
    private static readonly DateTime ModTime = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string VaultPath = @"C:\Vault";
    private const string FilePath = @"C:\Vault\my-note.md";

    // ── Title fallback order ────────────────────────────────────────────────────

    [Fact]
    public void Title_FromFrontmatterTitle()
    {
        var note = Parse("---\ntitle: Frontmatter Title\n---\n# Ignored Heading\nBody.");
        Assert.Equal("Frontmatter Title", note.Title);
    }

    [Fact]
    public void Title_FromFirstH1WhenNoFrontmatterTitle()
    {
        var note = Parse("# My Heading\n\nSome body text.");
        Assert.Equal("My Heading", note.Title);
    }

    [Fact]
    public void Title_FallsBackToFilename()
    {
        var note = Parse("Just a paragraph, no heading.");
        Assert.Equal("my-note", note.Title);
    }

    [Fact]
    public void Title_FrontmatterTitleQuotesStripped()
    {
        var note = Parse("---\ntitle: \"Quoted Title\"\n---\nBody.");
        Assert.Equal("Quoted Title", note.Title);
    }

    // ── Tag parsing ─────────────────────────────────────────────────────────────

    [Fact]
    public void Tags_FromFrontmatterList()
    {
        var note = Parse("---\ntags:\n  - project\n  - work\n---\nBody.");
        Assert.Contains("project", note.Tags);
        Assert.Contains("work", note.Tags);
    }

    [Fact]
    public void Tags_FromFrontmatterCsv()
    {
        var note = Parse("---\ntags: project, work\n---\nBody.");
        Assert.Contains("project", note.Tags);
        Assert.Contains("work", note.Tags);
    }

    [Fact]
    public void Tags_FromFrontmatterInlineArray()
    {
        var note = Parse("---\ntags: [project, work, health]\n---\nBody.");
        Assert.Contains("project", note.Tags);
        Assert.Contains("health", note.Tags);
    }

    [Fact]
    public void Tags_FromInlineBodyHashtags()
    {
        var note = Parse("Meeting notes. #project #work");
        Assert.Contains("project", note.Tags);
        Assert.Contains("work", note.Tags);
    }

    [Fact]
    public void Tags_Deduplicated_AcrossFrontmatterAndInline()
    {
        var note = Parse("---\ntags: project\n---\n#project mentioned in body.");
        Assert.Single(note.Tags, t => t == "project");
    }

    // ── Alias parsing ────────────────────────────────────────────────────────────

    [Fact]
    public void Aliases_FromFrontmatterList()
    {
        var note = Parse("---\naliases:\n  - Alt Name\n  - Other\n---\nBody.");
        Assert.Contains("Alt Name", note.Aliases);
        Assert.Contains("Other", note.Aliases);
    }

    [Fact]
    public void Aliases_FromSingularAliasCsv()
    {
        var note = Parse("---\nalias: AltA, AltB\n---\nBody.");
        Assert.Contains("AltA", note.Aliases);
        Assert.Contains("AltB", note.Aliases);
    }

    // ── Heading extraction ────────────────────────────────────────────────────

    [Fact]
    public void Headings_ExtractedFromAllLevels()
    {
        var note = Parse("# H1\n## H2\n### H3\nBody.");
        Assert.Contains("H1", note.Headings);
        Assert.Contains("H2", note.Headings);
        Assert.Contains("H3", note.Headings);
    }

    [Fact]
    public void Headings_Empty_WhenNone()
    {
        var note = Parse("Just body text, no headings.");
        Assert.Empty(note.Headings);
    }

    // ── Wiki-link extraction ─────────────────────────────────────────────────

    [Fact]
    public void WikiLinks_SimpleLink()
    {
        var links = ObsidianMarkdownParser.ExtractWikiLinks("See [[My Note]] for details.");
        Assert.Contains("My Note", links);
    }

    [Fact]
    public void WikiLinks_LinkWithDisplay()
    {
        var links = ObsidianMarkdownParser.ExtractWikiLinks("[[My Note|Click here]]");
        Assert.Contains("My Note", links);
        Assert.DoesNotContain("Click here", links);
    }

    [Fact]
    public void WikiLinks_LinkWithHeading()
    {
        var links = ObsidianMarkdownParser.ExtractWikiLinks("[[My Note#Section]]");
        Assert.Contains("My Note", links);
    }

    [Fact]
    public void WikiLinks_FolderPath()
    {
        var links = ObsidianMarkdownParser.ExtractWikiLinks("[[folder/Sub Note]]");
        Assert.Contains("folder/Sub Note", links);
    }

    [Fact]
    public void WikiLinks_Deduplicated()
    {
        var links = ObsidianMarkdownParser.ExtractWikiLinks("[[Note A]] and [[Note A]] again.");
        Assert.Single(links, l => l == "Note A");
    }

    [Fact]
    public void WikiLinks_Empty_WhenNone()
    {
        var links = ObsidianMarkdownParser.ExtractWikiLinks("No links here.");
        Assert.Empty(links);
    }

    // ── Slug / filename creation ──────────────────────────────────────────────

    [Theory]
    [InlineData("My Note Title", "my-note-title")]
    [InlineData("Café au lait", "cafe-au-lait")]
    [InlineData("  leading spaces  ", "leading-spaces")]
    [InlineData("Special!@#Chars", "special-chars")]
    [InlineData("", "")]
    public void Slugify_VariousInputs(string input, string expected)
    {
        Assert.Equal(expected, ObsidianMarkdownParser.Slugify(input));
    }

    [Fact]
    public void Slugify_RespectsMaxLength()
    {
        string longTitle = new string('a', 200);
        string slug = ObsidianMarkdownParser.Slugify(longTitle, maxLength: 10);
        Assert.True(slug.Length <= 10);
    }

    // ── Body and relative path ────────────────────────────────────────────────

    [Fact]
    public void Body_FrontmatterNotIncludedInBody()
    {
        var note = Parse("---\ntitle: Test\n---\nThis is the body.");
        Assert.DoesNotContain("---", note.Body);
        Assert.Contains("This is the body.", note.Body);
    }

    [Fact]
    public void RelativePath_IsRelativeToVault()
    {
        var note = ObsidianMarkdownParser.ParseMarkdown(
            @"C:\Vault\subfolder\note.md",
            @"C:\Vault",
            "# Hello",
            ModTime);
        Assert.Equal(@"subfolder\note.md", note.RelativePath);
    }

    // ── URI service ───────────────────────────────────────────────────────────

    [Fact]
    public void UriService_OpenNote_RemovesMdExtension()
    {
        string uri = ObsidianUriService.OpenNote("MyVault", "subfolder/note.md");
        Assert.Contains("subfolder%2Fnote", uri);
        Assert.DoesNotContain(".md", uri);
    }

    [Fact]
    public void UriService_OpenNote_EscapesSpecialChars()
    {
        string uri = ObsidianUriService.OpenNote("My Vault", "My Note.md");
        Assert.Contains("My%20Vault", uri);
    }

    [Fact]
    public void UriService_DailyNote_CorrectScheme()
    {
        string uri = ObsidianUriService.DailyNote("TestVault");
        Assert.StartsWith("obsidian://daily?vault=TestVault", uri);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static NpuTools.Obsidian.Models.ObsidianNote Parse(string markdown)
        => ObsidianMarkdownParser.ParseMarkdown(FilePath, VaultPath, markdown, ModTime);
}
