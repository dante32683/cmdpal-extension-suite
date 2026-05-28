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

    [Fact]
    public void ParseMarkdown_PreservesCreatedUtcFromFrontmatter()
    {
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var updated = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero);
        string markdown = $"---\nid: abc\ntitle: Round-trip\ncategory: misc\ncreatedUtc: {created:O}\nupdatedUtc: {updated:O}\ntags:\n---\n\n# Round-trip\n\nBody.";

        var entry = NotesStore.ParseMarkdown(@"C:\notes\misc\test.md", @"C:\notes", markdown, DateTime.UtcNow, DateTime.UtcNow);

        Assert.Equal(created, entry.CreatedUtc);
        Assert.Equal(updated, entry.UpdatedUtc);
    }

    [Fact]
    public void UpdateNote_OverwritesTitleAndBody_PreservesCreatedUtc()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NpuNotesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            string originalMarkdown = $"---\nid: test-id\ntitle: Original Title\ncategory: misc\ncreatedUtc: {created:O}\nupdatedUtc: {created:O}\ntags:\n---\n\n# Original Title\n\nOriginal body.";
            string filePath = Path.Combine(dir, "test.md");
            File.WriteAllText(filePath, originalMarkdown);

            var entry = NotesStore.ParseMarkdown(filePath, dir, originalMarkdown, DateTime.UtcNow, DateTime.UtcNow);
            var settings = new NotesSettingsStore();
            var index = new NotesIndexStore(settings);
            var store = new NotesStore(settings, index);

            store.UpdateNote(entry, "Updated Title", "Updated body.");

            string written = File.ReadAllText(filePath);
            var updated = NotesStore.ParseMarkdown(filePath, dir, written, DateTime.UtcNow, DateTime.UtcNow);

            Assert.Equal("Updated Title", updated.Title);
            Assert.Contains("Updated body.", updated.Body);
            Assert.Equal(created, updated.CreatedUtc);
            Assert.True(updated.UpdatedUtc > created);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void UpdateNote_SkipsWrite_WhenNoteWasModifiedAfterRead()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NpuNotesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var laterEdit = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
            // On disk: note was edited at laterEdit
            string editedMarkdown = $"---\nid: test-id\ntitle: Manually Edited\ncategory: misc\ncreatedUtc: {created:O}\nupdatedUtc: {laterEdit:O}\ntags:\n---\n\n# Manually Edited\n\nHuman edits.";
            string filePath = Path.Combine(dir, "test.md");
            File.WriteAllText(filePath, editedMarkdown);

            // Entry snapshot from before the edit (stale)
            string staleMarkdown = $"---\nid: test-id\ntitle: Original\ncategory: misc\ncreatedUtc: {created:O}\nupdatedUtc: {created:O}\ntags:\n---\n\n# Original\n\nOriginal body.";
            var staleEntry = NotesStore.ParseMarkdown(filePath, dir, staleMarkdown, DateTime.UtcNow, DateTime.UtcNow);

            var settings = new NotesSettingsStore();
            var index = new NotesIndexStore(settings);
            var store = new NotesStore(settings, index);

            store.UpdateNote(staleEntry, "AI Overwrite", "AI cleaned body.");

            string current = File.ReadAllText(filePath);
            Assert.Contains("Manually Edited", current);
            Assert.DoesNotContain("AI Overwrite", current);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryReadUpdatedUtc_ReadsFromFrontmatter()
    {
        string dir = Path.Combine(Path.GetTempPath(), "NpuNotesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var expected = new DateTimeOffset(2026, 5, 27, 10, 30, 0, TimeSpan.Zero);
            string markdown = $"---\nid: x\ntitle: T\ncategory: misc\ncreatedUtc: {expected:O}\nupdatedUtc: {expected:O}\ntags:\n---\n\n# T\n";
            string filePath = Path.Combine(dir, "t.md");
            File.WriteAllText(filePath, markdown);

            bool found = NotesStore.TryReadUpdatedUtc(filePath, out var actual);

            Assert.True(found);
            Assert.Equal(expected.ToUniversalTime(), actual.ToUniversalTime());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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
