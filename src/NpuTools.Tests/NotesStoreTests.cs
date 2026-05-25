using NpuTools.Notes.Models;
using NpuTools.Notes.Services;
using Xunit;

namespace NpuTools.Tests;

public class NotesStoreTests
{
    [Fact]
    public void ParseRawNote_FirstLineBecomesTitle()
    {
        var parsed = NotesStore.ParseRawNote("Project Plan\nShip the notes extension.");

        Assert.Equal("Project Plan", parsed.Title);
        Assert.Equal("Ship the notes extension.", parsed.Body);
    }

    [Fact]
    public void ParseRawNote_BlankTextUsesUntitled()
    {
        var parsed = NotesStore.ParseRawNote("   ");

        Assert.Equal("Untitled Note", parsed.Title);
        Assert.Equal(string.Empty, parsed.Body);
    }

    [Fact]
    public void ParseMarkdown_UsesFrontmatterTitleAndCategory()
    {
        string markdown = """
---
id: note-1
title: Manager 1:1
category: work
createdUtc: 2026-05-24T10:00:00.0000000Z
updatedUtc: 2026-05-24T10:05:00.0000000Z
tags: people, meetings
---

# Ignored Heading

Body text
""";

        var entry = NotesStore.ParseMarkdown(
            @"C:\notes\work\manager.md",
            @"C:\notes",
            markdown,
            new DateTime(2026, 5, 24, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 24, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal("note-1", entry.Id);
        Assert.Equal("Manager 1:1", entry.Title);
        Assert.Equal("work", entry.Category);
        Assert.Equal(["people", "meetings"], entry.Tags);
        Assert.Contains("Body text", entry.Body);
    }

    [Fact]
    public void ParseMarkdown_FallsBackToFirstHeading()
    {
        var entry = NotesStore.ParseMarkdown(
            @"C:\notes\ideas\idea.md",
            @"C:\notes",
            "# Better Search\n\nBody",
            DateTime.UtcNow,
            DateTime.UtcNow);

        Assert.Equal("Better Search", entry.Title);
        Assert.Equal("ideas", entry.Category);
    }

    [Theory]
    [InlineData("Ideas", "ideas")]
    [InlineData("unknown", "misc")]
    [InlineData("", "misc")]
    public void NormalizeCategory_UsesKnownCategories(string input, string expected)
    {
        Assert.Equal(expected, NotesStore.NormalizeCategory(input));
    }

    [Fact]
    public void Slugify_NormalizesTitle()
    {
        Assert.Equal("resume-notes-2026", NotesStore.Slugify("Résumé Notes 2026!"));
    }
}

public class NotesSearchServiceTests
{
    [Fact]
    public void Search_TitleMatchBeatsBodyMatch()
    {
        var service = new NotesSearchService();
        var title = new NoteEntry { Title = "Budget Review", Body = "Nothing", UpdatedUtc = DateTimeOffset.UtcNow };
        var body = new NoteEntry { Title = "Meeting", Body = "budget details", UpdatedUtc = DateTimeOffset.UtcNow };

        var results = service.Search([body, title], "budget", 10);

        Assert.Same(title, results[0]);
    }

    [Fact]
    public void Search_PinnedBoostsResults()
    {
        var service = new NotesSearchService();
        var regular = new NoteEntry { Title = "Search Notes", Body = "cmdpal", UpdatedUtc = DateTimeOffset.UtcNow };
        var pinned = new NoteEntry { Title = "Search Notes", Body = "cmdpal", IsPinned = true, UpdatedUtc = DateTimeOffset.UtcNow.AddDays(-1) };

        var results = service.Search([regular, pinned], "cmdpal", 10);

        Assert.Same(pinned, results[0]);
    }
}
