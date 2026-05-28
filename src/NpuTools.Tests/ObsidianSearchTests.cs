using System;
using System.Collections.Generic;
using NpuTools.Obsidian.Models;
using NpuTools.Obsidian.Services;
using Xunit;

namespace NpuTools.Tests;

public sealed class ObsidianSearchTests
{
    private static ObsidianNote Note(
        string title,
        string body = "",
        string[]? tags = null,
        string[]? aliases = null,
        string[]? headings = null,
        string[]? backlinks = null,
        bool pinned = false,
        string relativePath = "note.md")
    {
        return new ObsidianNote
        {
            AbsolutePath = $@"C:\Vault\{relativePath}",
            RelativePath = relativePath,
            VaultPath = @"C:\Vault",
            Title = title,
            Body = body,
            Tags = tags is not null ? new List<string>(tags) : [],
            Aliases = aliases is not null ? new List<string>(aliases) : [],
            Headings = headings is not null ? new List<string>(headings) : [],
            Backlinks = backlinks is not null ? new List<string>(backlinks) : [],
            IsPinned = pinned,
            LastModifiedUtc = DateTimeOffset.UtcNow,
        };
    }

    private readonly ObsidianSearchService _search = new();

    // ── Scoring: title beats body ─────────────────────────────────────────────

    [Fact]
    public void TitleMatch_ScoresHigherThan_BodyMatch()
    {
        int titleScore = ObsidianSearchService.Score(Note("cats and dogs", ""), "cats");
        int bodyScore = ObsidianSearchService.Score(Note("unrelated title", "cats mentioned in body"), "cats");
        Assert.True(titleScore > bodyScore);
    }

    // ── Scoring: alias beats tags ─────────────────────────────────────────────

    [Fact]
    public void AliasMatch_ScoresHigherThan_TagMatch()
    {
        int aliasScore = ObsidianSearchService.Score(Note("title", aliases: ["query"]), "query");
        int tagScore = ObsidianSearchService.Score(Note("title", tags: ["query"]), "query");
        Assert.True(aliasScore > tagScore);
    }

    // ── Scoring: whole-word bonus ─────────────────────────────────────────────

    [Fact]
    public void WholeWordTitleMatch_ScoresHigherThan_SubstringMatch()
    {
        int wholeWord = ObsidianSearchService.Score(Note("cat"), "cat");
        int substring = ObsidianSearchService.Score(Note("category"), "cat");
        Assert.True(wholeWord > substring);
    }

    // ── Scoring: pinned bonus ─────────────────────────────────────────────────

    [Fact]
    public void PinnedNote_ScoresHigherThanUnpinned_OnSameQuery()
    {
        int pinned = ObsidianSearchService.Score(Note("cats", pinned: true), "cats");
        int unpinned = ObsidianSearchService.Score(Note("cats", pinned: false), "cats");
        Assert.True(pinned > unpinned);
    }

    // ── Scoring: backlink bonus ───────────────────────────────────────────────

    [Fact]
    public void BacklinksIncrease_Score()
    {
        int withBacklinks = ObsidianSearchService.Score(Note("cats", backlinks: ["other.md", "third.md"]), "cats");
        int withoutBacklinks = ObsidianSearchService.Score(Note("cats"), "cats");
        Assert.True(withBacklinks > withoutBacklinks);
    }

    [Fact]
    public void BacklinkBonus_CappedAtThree()
    {
        int manyBacklinks = ObsidianSearchService.Score(Note("cats", backlinks: ["a.md", "b.md", "c.md", "d.md", "e.md"]), "cats");
        int threeBacklinks = ObsidianSearchService.Score(Note("cats", backlinks: ["a.md", "b.md", "c.md"]), "cats");
        Assert.Equal(manyBacklinks, threeBacklinks);
    }

    // ── Scoring: exact title beats substring ──────────────────────────────────

    [Fact]
    public void ExactTitleMatch_ScoresHigherThan_TitleSubstringMatch()
    {
        int exact     = ObsidianSearchService.Score(Note("cat"), "cat");
        int substring = ObsidianSearchService.Score(Note("cathedral notes"), "cat");
        Assert.True(exact > substring, $"exact={exact} should beat substring={substring}");
    }

    [Fact]
    public void ExactTitleMatch_BeatsSubstring_EvenWithBacklinkDisadvantage()
    {
        // "cathedral" has 3 extra backlink points but exact-title "cat" should still win.
        int exact     = ObsidianSearchService.Score(Note("cat"), "cat");
        int substring = ObsidianSearchService.Score(Note("cathedral notes", backlinks: ["a.md", "b.md", "c.md"]), "cat");
        Assert.True(exact > substring, $"exact={exact} should beat substring+backlinks={substring}");
    }

    // ── Scoring: zero for non-matching ────────────────────────────────────────

    [Fact]
    public void NonMatchingNote_ScoresZero()
    {
        int score = ObsidianSearchService.Score(Note("apples", "fruit salad"), "cats");
        Assert.Equal(0, score);
    }

    // ── Search ordering ───────────────────────────────────────────────────────

    [Fact]
    public void Search_ReturnsHighestScoringFirst()
    {
        var notes = new List<ObsidianNote>
        {
            Note("body mention", "cats are nice"),          // body match (+2)
            Note("cats are great", ""),                     // title substring match (+10 + whole-word +2)
            Note("no match", ""),
        };
        var service = new ObsidianSearchService();
        var results = service.Search(notes, "cats", 10);

        Assert.Equal(2, results.Count);
        Assert.Equal("cats are great", results[0].Title);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsPinnedFirst()
    {
        var unpinned = Note("Recent Note", pinned: false);
        unpinned.LastOpenedUtc = DateTimeOffset.UtcNow;

        var pinned = Note("Pinned Note", pinned: true);
        pinned.PinOrder = 0;

        var notes = new List<ObsidianNote> { unpinned, pinned };
        var service = new ObsidianSearchService();
        var results = service.Search(notes, "", 10);

        Assert.Equal("Pinned Note", results[0].Title);
    }

    [Fact]
    public void Search_MaxResults_Respected()
    {
        var notes = new List<ObsidianNote>
        {
            Note("cats a"), Note("cats b"), Note("cats c"), Note("cats d"),
        };
        var service = new ObsidianSearchService();
        var results = service.Search(notes, "cats", 2);

        Assert.Equal(2, results.Count);
    }

    // ── AI summary scoring ────────────────────────────────────────────────────

    [Fact]
    public void AiSummaryMatch_AddsScore()
    {
        var withSummary = Note("unrelated title");
        withSummary.AiSummary = "This note is about cats";
        int scoreWithSummary = ObsidianSearchService.Score(withSummary, "cats");

        var withoutSummary = Note("unrelated title");
        int scoreWithout = ObsidianSearchService.Score(withoutSummary, "cats");

        Assert.True(scoreWithSummary > scoreWithout);
    }
}
