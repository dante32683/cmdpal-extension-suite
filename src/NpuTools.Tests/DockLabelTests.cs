using ActionCenterExtension.Services;
using Xunit;

namespace NpuTools.Tests;

public sealed class DockLabelTests
{
    [Fact]
    public void ShortTitleGetsLeadingSpaces()
    {
        var padded = DockLabel.PadToCenter("AI");

        Assert.EndsWith("AI", padded);
        Assert.True(padded.Length - 2 >= 3, "a very short title needs several spaces to reach the middle");
        Assert.All(padded[..^2], c => Assert.Equal(' ', c));
    }

    [Fact]
    public void LongerTitlesAreUnchanged()
    {
        Assert.Equal("Terminal", DockLabel.PadToCenter("Terminal"));
        Assert.Equal("Personal", DockLabel.PadToCenter("Personal"));
    }

    [Fact]
    public void PaddingShrinksAsTitlesGetLonger()
    {
        static int Pad(string s) => DockLabel.PadToCenter(s).Length - s.Length;

        Assert.True(Pad("AI") > Pad("Work"));
        Assert.True(Pad("Work") >= Pad("Music"));
        Assert.Equal(0, Pad("Terminal"));
    }

    [Fact]
    public void EmptyTitleDoesNotThrow()
    {
        Assert.Equal(string.Empty, DockLabel.PadToCenter("").Trim());
    }
}
