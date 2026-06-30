using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NpuTools.Clipboard.Data;
using Xunit;

namespace NpuTools.Tests;

public sealed class SecretPatternMatcherTests
{
    [Fact]
    public void Matcher_DefaultsDetectCloudflareApiKey()
    {
        var matcher = new SecretPatternMatcher(new ClipboardAppSettings());
        Assert.True(matcher.Enabled);
        Assert.Equal("Cloudflare API Key", matcher.Match("export CF_API_KEY=abcd1234efgh5678ijkl9012mnop"));
    }

    [Fact]
    public void Matcher_DefaultsDetectCloudflareApiToken()
    {
        var matcher = new SecretPatternMatcher(new ClipboardAppSettings());
        Assert.Equal("Cloudflare API Token", matcher.Match("CF_API_TOKEN=abcd1234efgh5678ijkl9012mnop3456"));
    }

    [Fact]
    public void Matcher_DefaultsDetectGitHubPat()
    {
        var matcher = new SecretPatternMatcher(new ClipboardAppSettings());
        // ghp_ + 36 base62 chars
        string token = "ghp_" + new string('a', 36);
        Assert.Equal("GitHub PAT", matcher.Match("Authorization: Bearer " + token));
    }

    [Fact]
    public void Matcher_DefaultsDetectAwsAccessKey()
    {
        var matcher = new SecretPatternMatcher(new ClipboardAppSettings());
        Assert.Equal("AWS Access Key ID", matcher.Match("aws_access_key_id=AKIAIOSFODNN7EXAMPLE"));
    }

    [Fact]
    public void Matcher_DisabledReturnsNullForAnyText()
    {
        var settings = new ClipboardAppSettings { SecretDetectionEnabled = false };
        var matcher = new SecretPatternMatcher(settings);
        Assert.False(matcher.Enabled);
        Assert.Null(matcher.Match("export CF_API_KEY=abcd1234efgh5678ijkl9012mnop"));
    }

    [Fact]
    public void Matcher_EmptyTextReturnsNull()
    {
        var matcher = new SecretPatternMatcher(new ClipboardAppSettings());
        Assert.Null(matcher.Match(""));
        Assert.Null(matcher.Match(null));
    }

    [Fact]
    public void Matcher_PlainTextDoesNotMatch()
    {
        var matcher = new SecretPatternMatcher(new ClipboardAppSettings());
        Assert.Null(matcher.Match("Hello, world!"));
        Assert.Null(matcher.Match("The quick brown fox jumps over the lazy dog."));
    }

    [Fact]
    public void Matcher_EmptyPatternListReturnsNull()
    {
        var settings = new ClipboardAppSettings { SecretPatterns = new List<SecretPattern>() };
        var matcher = new SecretPatternMatcher(settings);
        Assert.True(matcher.Enabled);
        Assert.Equal(0, matcher.CompiledCount);
        Assert.Null(matcher.Match("export CF_API_KEY=abcd1234efgh5678ijkl9012mnop"));
    }

    [Fact]
    public void Matcher_MalformedPatternIsSkippedAtConstruction()
    {
        var settings = new ClipboardAppSettings
        {
            SecretPatterns = new List<SecretPattern>
            {
                new() { Name = "Bad",     Regex = "(unclosed" },
                new() { Name = "Good",    Regex = @"AKIA[0-9A-Z]{16}" },
                new() { Name = "Empty",   Regex = "" },
            },
        };
        var matcher = new SecretPatternMatcher(settings);
        Assert.Equal(1, matcher.CompiledCount);
        Assert.Equal("Good", matcher.Match("AKIAIOSFODNN7EXAMPLE"));
    }

    [Fact]
    public void Matcher_CustomPatternFires()
    {
        // Fictional token format to avoid tripping GitHub's secret-scanner push
        // protection (real prefixes like sk_live_/sk_test_/AKIA are rejected
        // on push even when used as test fixtures).
        var settings = new ClipboardAppSettings
        {
            SecretPatterns = new List<SecretPattern>
            {
                new() { Name = "Acme Live", Regex = @"acme_live_[A-Za-z0-9]{8,}" },
            },
        };
        var matcher = new SecretPatternMatcher(settings);
        Assert.Equal("Acme Live", matcher.Match("acme_live_abcdefgh1"));
        Assert.Null(matcher.Match("acme_test_abcdefgh1"));
    }

    [Fact]
    public void FormParser_ParsesValidLines()
    {
        string raw = "Cloudflare API Key | CF_API_KEY=\\w{20,}\nGitHub PAT | \\bghp_[A-Za-z0-9]{36}\\b";
        var (patterns, invalid) = SecretPatternsParser.ParseLines(raw);
        Assert.Equal(2, patterns.Count);
        Assert.Equal(0, invalid);
        Assert.Equal("Cloudflare API Key", patterns[0].Name);
        Assert.Equal(@"CF_API_KEY=\w{20,}", patterns[0].Regex);
    }

    [Fact]
    public void FormParser_SkipsInvalidFormatAndBadRegex()
    {
        string raw = "\nGood | AKIA[0-9A-Z]{16}\nNo separator here\n| missing name\nmissing regex |\nBad regex | (unclosed";
        var (patterns, invalid) = SecretPatternsParser.ParseLines(raw);
        Assert.Single(patterns);
        Assert.Equal("Good", patterns[0].Name);
        Assert.Equal(4, invalid);
    }

    [Fact]
    public void FormParser_EmptyPayloadProducesEmptyList()
    {
        var (patterns, invalid) = SecretPatternsParser.ParseLines("");
        Assert.Empty(patterns);
        Assert.Equal(0, invalid);
    }

    [Fact]
    public void RoundTrip_SecretPatternJson_PreservesNameAndRegex()
    {
        var list = new List<SecretPattern>
        {
            new() { Name = "Test", Regex = @"AKIA[0-9A-Z]{16}" },
        };
        string json = JsonSerializer.Serialize(list, ClipboardJsonContext.Default.ListSecretPattern);
        var roundTripped = JsonSerializer.Deserialize(json, ClipboardJsonContext.Default.ListSecretPattern);
        Assert.NotNull(roundTripped);
        Assert.Single(roundTripped!);
        Assert.Equal("Test", roundTripped![0].Name);
        Assert.Equal(@"AKIA[0-9A-Z]{16}", roundTripped[0].Regex);
    }
}
