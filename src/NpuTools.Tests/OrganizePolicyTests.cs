using NpuTools.Organize.Services;
using Xunit;

namespace NpuTools.Tests;

public sealed class OrganizePolicyTests
{
    [Theory]
    [InlineData("2026-09-14_description-slug.png", true)]
    [InlineData("2026-09-14_description-slug-2.jpg", true)]
    [InlineData("Screenshot 2026-09-14 123456.png", false)]
    [InlineData("2026-09-14.png", false)]
    [InlineData("2026-09-14_.png", false)]
    public void IsAlreadyOrganized_RequiresDateAndNonemptySlug(string fileName, bool expected)
    {
        Assert.Equal(expected, SlugService.IsAlreadyOrganized(fileName));
    }

    [Fact]
    public void BuildProposedPath_UsesCanonicalFallbackWhenNameHasNoSlug()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"npu-organize-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{DateTime.Today:yyyy-MM-dd}.png");
        try
        {
            File.WriteAllBytes(path, []);
            string proposed = SlugService.BuildProposedPath(path);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}_screenshot\.png$", Path.GetFileName(proposed));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("{\"skipOnBattery\":true}", true)]
    [InlineData("{\"skipOnBattery\":false}", false)]
    [InlineData("{}", true)]
    [InlineData("not json", true)]
    public void IsSkipOnBatteryEnabled_UsesSafeDefault(string json, bool expected)
    {
        string path = Path.Combine(Path.GetTempPath(), $"npu-organize-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            Assert.Equal(expected, OrganizeSettings.IsSkipOnBatteryEnabled(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
