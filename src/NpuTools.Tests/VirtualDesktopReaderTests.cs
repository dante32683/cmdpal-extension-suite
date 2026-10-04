using ActionCenterExtension.Services;
using Microsoft.Win32;
using Xunit;

namespace NpuTools.Tests;

public sealed class VirtualDesktopReaderTests : IDisposable
{
    private readonly string _keyPath = $@"Software\NpuToolsTests\VirtualDesktops-{Guid.NewGuid():N}";

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

    private static byte[] Blob(params Guid[] ids) => ids.SelectMany(g => g.ToByteArray()).ToArray();

    [Fact]
    public void ResolveReturnsNameAndPositionOfCurrentDesktop()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var info = VirtualDesktopReader.Resolve(b.ToByteArray(), Blob(a, b, c), id => id == b ? "Work" : null);

        Assert.Equal(new VirtualDesktopInfo("Work", 2, 3), info);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveFallsBackToNumberedNameWhenUnnamed(string? name)
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var info = VirtualDesktopReader.Resolve(b.ToByteArray(), Blob(a, b), _ => name);

        Assert.Equal(new VirtualDesktopInfo("Desktop 2", 2, 2), info);
    }

    [Fact]
    public void ResolveReturnsDesktopOneWhenCurrentIdIsNotInList()
    {
        var info = VirtualDesktopReader.Resolve(Guid.NewGuid().ToByteArray(), Blob(Guid.NewGuid(), Guid.NewGuid()), _ => "Ignored");

        Assert.Equal(new VirtualDesktopInfo("Desktop 1", 1, 2), info);
    }

    [Fact]
    public void ResolveHandlesMissingOrMalformedData()
    {
        var expected = new VirtualDesktopInfo("Desktop 1", 1, 1);

        Assert.Equal(expected, VirtualDesktopReader.Resolve(null, null, _ => null));
        Assert.Equal(expected, VirtualDesktopReader.Resolve(new byte[5], Blob(Guid.NewGuid()), _ => null));
        Assert.Equal(expected, VirtualDesktopReader.Resolve(Guid.NewGuid().ToByteArray(), [], _ => null));
    }

    [Fact]
    public void ReadReturnsFallbackWhenKeyIsMissing()
    {
        var info = new VirtualDesktopReader(_keyPath).Read();

        Assert.Equal(new VirtualDesktopInfo("Desktop 1", 1, 1), info);
    }

    [Fact]
    public void ReadReflectsRegistryChanges()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        using (var root = Registry.CurrentUser.CreateSubKey(_keyPath))
        {
            root.SetValue("VirtualDesktopIDs", Blob(first, second), RegistryValueKind.Binary);
            root.SetValue("CurrentVirtualDesktop", first.ToByteArray(), RegistryValueKind.Binary);
            using var named = root.CreateSubKey($@"Desktops\{second:B}");
            named.SetValue("Name", "Chats");
        }

        var reader = new VirtualDesktopReader(_keyPath);
        Assert.Equal(new VirtualDesktopInfo("Desktop 1", 1, 2), reader.Read());

        using (var root = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true)!)
        {
            root.SetValue("CurrentVirtualDesktop", second.ToByteArray(), RegistryValueKind.Binary);
        }

        Assert.Equal(new VirtualDesktopInfo("Chats", 2, 2), reader.Read());
    }
}
