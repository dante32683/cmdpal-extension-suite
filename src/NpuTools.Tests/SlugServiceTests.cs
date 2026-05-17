using NpuOrganizeKeeper;
using Xunit;

namespace NpuTools.Tests;

/// <summary>
/// Tests the slug algorithm from NpuOrganizeKeeper.SlugGenerator against
/// known inputs/outputs, verifying parity with the Raycast TypeScript slug.ts rules.
/// </summary>
public sealed class SlugServiceTests
{
    // ── Slugify ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("A cat sitting on a mat", "cat-sitting-mat")]            // stopwords filtered
    [InlineData("Windows desktop screenshot", "windows-desktop")]        // "screenshot" is stopword
    [InlineData("The quick brown fox", "quick-brown-fox")]               // leading "the" stripped
    [InlineData("", "")]                                                  // empty → empty
    [InlineData("   ", "")]                                               // whitespace → empty
    [InlineData("image of a phone", "phone")]                            // "image", "of", "a" all stopwords
    public void Slugify_FilteredInputs(string description, string expected)
    {
        string result = SlugGenerator.Slugify(description);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Slugify_MaxFiveTokensByDefault()
    {
        // Six meaningful words → only first five kept.
        string input = "cat dog fox bear wolf eagle";
        string result = SlugGenerator.Slugify(input);
        Assert.Equal("cat-dog-fox-bear-wolf", result);
    }

    [Fact]
    public void Slugify_AllStopwords_FallsBackToRawTokens()
    {
        // All words are stopwords → raw tokens used, capped at 5.
        string input = "a an the of is";
        string result = SlugGenerator.Slugify(input);
        // Falls back to raw tokens when all filtered out.
        Assert.Equal("a-an-the-of-is", result);
    }

    [Fact]
    public void Slugify_NormalizesUnicode()
    {
        // Accented characters should be stripped to ASCII equivalents.
        string input = "café résumé naïve";
        string result = SlugGenerator.Slugify(input);
        // "café" → "cafe", "résumé" → "resume", "naïve" → "naive"
        Assert.Equal("cafe-resume-naive", result);
    }

    [Fact]
    public void Slugify_TruncatesToMaxLength()
    {
        // Very long single token should be truncated.
        string input = new string('a', 200);
        string result = SlugGenerator.Slugify(input, maxLength: 60);
        Assert.True(result.Length <= 60);
        Assert.False(result.EndsWith('-'));
    }

    [Fact]
    public void Slugify_NonAlphanumericReplacedWithHyphen()
    {
        string input = "hello, world! foo-bar";
        string result = SlugGenerator.Slugify(input);
        // punctuation becomes spaces then slug separators
        Assert.DoesNotContain(",", result);
        Assert.DoesNotContain("!", result);
    }

    // ── BuildTargetFilename ────────────────────────────────────────────────────

    [Fact]
    public void BuildTargetFilename_FormatsCorrectly()
    {
        string result = SlugGenerator.BuildTargetFilename("cat-on-mat", ".png", new DateTime(2025, 6, 15));
        Assert.Equal("2025-06-15_cat-on-mat.png", result);
    }

    [Fact]
    public void BuildTargetFilename_NormalizesExtension()
    {
        string result = SlugGenerator.BuildTargetFilename("test", "PNG", new DateTime(2025, 1, 1));
        Assert.Equal("2025-01-01_test.png", result);
    }

    [Fact]
    public void BuildTargetFilename_EmptySlugThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            SlugGenerator.BuildTargetFilename(string.Empty, ".png", DateTime.Today));
    }

    // ── IsAlreadyDateNamed ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("2025-06-15_cat-on-mat.png", true)]
    [InlineData("2025-06-15_screenshot.png", true)]
    [InlineData("Screenshot 2025-06-15 013016.png", false)]
    [InlineData("Screenshot.png", false)]
    [InlineData("2025-06-15.png", false)]  // date only, no underscore at position 10
    public void IsAlreadyDateNamed_DetectsOrganizedFiles(string basename, bool expected)
    {
        bool result = SlugGenerator.IsAlreadyDateNamed(basename);
        Assert.Equal(expected, result);
    }

    // ── ResolveCollision ───────────────────────────────────────────────────────

    [Fact]
    public void ResolveCollision_NoConflict_ReturnsOriginal()
    {
        string result = SlugGenerator.ResolveCollision("test.png", _ => false);
        Assert.Equal("test.png", result);
    }

    [Fact]
    public void ResolveCollision_FirstConflict_ReturnsNumbered()
    {
        var taken = new HashSet<string> { "test.png" };
        string result = SlugGenerator.ResolveCollision("test.png", taken.Contains);
        Assert.Equal("test-2.png", result);
    }

    [Fact]
    public void ResolveCollision_MultipleConflicts_AdvancesCounter()
    {
        var taken = new HashSet<string> { "test.png", "test-2.png", "test-3.png" };
        string result = SlugGenerator.ResolveCollision("test.png", taken.Contains);
        Assert.Equal("test-4.png", result);
    }

    // ── NormalizeExtension ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(".png",  ".png")]
    [InlineData("png",   ".png")]
    [InlineData("PNG",   ".png")]
    [InlineData(".PNG",  ".png")]
    [InlineData("",      "")]
    public void NormalizeExtension_LowercasesAndPrependsDot(string input, string expected)
    {
        Assert.Equal(expected, SlugGenerator.NormalizeExtension(input));
    }
}
