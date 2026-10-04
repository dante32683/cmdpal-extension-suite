using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ActionCenterExtension.Services;

/// <summary>The active virtual desktop: its display name and 1-based position among all desktops.</summary>
internal readonly record struct VirtualDesktopInfo(string Name, int Index, int Count);

/// <summary>
/// Reads the active virtual desktop from the per-user Explorer registry state.
/// The values are undocumented but stable since Windows 10; any failure falls back to "Desktop 1".
/// </summary>
internal sealed class VirtualDesktopReader
{
    internal const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    private static readonly VirtualDesktopInfo Fallback = new("Desktop 1", 1, 1);

    private readonly string _keyPath;

    public VirtualDesktopReader(string keyPath = DefaultKeyPath)
    {
        _keyPath = keyPath;
    }

    public VirtualDesktopInfo Read()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(_keyPath);
            if (root is null)
            {
                return Fallback;
            }

            var current = root.GetValue("CurrentVirtualDesktop") as byte[];
            var all = root.GetValue("VirtualDesktopIDs") as byte[];
            return Resolve(current, all, id => ReadName(root, id));
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            Debug.WriteLine($"Virtual desktop read failed: {ex}");
            return Fallback;
        }
    }

    /// <summary>Maps the raw registry GUID blobs to the active desktop's name and position.</summary>
    internal static VirtualDesktopInfo Resolve(byte[]? currentId, byte[]? allIds, Func<Guid, string?> nameOf)
    {
        const int GuidSize = 16;

        if (currentId is not { Length: GuidSize } || allIds is null || allIds.Length < GuidSize)
        {
            return Fallback;
        }

        var current = new Guid(currentId);
        var count = allIds.Length / GuidSize;
        var index = 0;
        for (var i = 0; i < count; i++)
        {
            if (new Guid(allIds.AsSpan(i * GuidSize, GuidSize)) == current)
            {
                index = i + 1;
                break;
            }
        }

        if (index == 0)
        {
            return new VirtualDesktopInfo("Desktop 1", 1, count);
        }

        var name = nameOf(current);
        return new VirtualDesktopInfo(string.IsNullOrWhiteSpace(name) ? $"Desktop {index}" : name, index, count);
    }

    private static string? ReadName(RegistryKey root, Guid id)
    {
        using var desktop = root.OpenSubKey($@"Desktops\{id:B}");
        return desktop?.GetValue("Name") as string;
    }
}
