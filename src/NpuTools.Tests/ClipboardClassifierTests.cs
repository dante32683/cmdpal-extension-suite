using NpuTools.Clipboard.Data;
using Xunit;

namespace NpuTools.Tests;

public sealed class ClipboardClassifierTests
{
    [Theory]
    [InlineData("https://example.com/a", ClipboardEntryKind.Link)]
    [InlineData("me@example.com", ClipboardEntryKind.Email)]
    [InlineData("#ff00cc", ClipboardEntryKind.Color)]
    [InlineData("rgb(255, 0, 204)", ClipboardEntryKind.Color)]
    [InlineData("ordinary note", ClipboardEntryKind.Text)]
    public void ClassifyText_DetectsExpectedKinds(string input, ClipboardEntryKind expected)
    {
        Assert.Equal(expected, ClipboardClassifier.ClassifyText(input));
    }

    [Fact]
    public void BuildTitle_TruncatesLongText()
    {
        string title = ClipboardClassifier.BuildTitle(ClipboardEntryKind.Text, new string('a', 100), 0);
        Assert.True(title.Length < 90);
        Assert.EndsWith("...", title);
    }
}
