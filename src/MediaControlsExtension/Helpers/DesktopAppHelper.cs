// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using JPSoftworks.MediaControlsExtension.Interop;

namespace JPSoftworks.MediaControlsExtension.Helpers;

internal static class DesktopAppHelper
{
    public static DesktopAppInfo? GetExecutable(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        return GetFromAppsFolder(appId) ?? GetFromRunningProcess(appId);
    }

    // Classic desktop apps such as Spotify report a bare executable name ("Spotify.exe") as their
    // app ID, but the Start menu's apps folder is keyed by the full exe path, so the lookup above misses.
    // The app is playing media, so its process is running and gives the real path.
    private static DesktopAppInfo? GetFromRunningProcess(string appId)
    {
        if (!appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || appId.AsSpan().ContainsAny('\\', '/', '!'))
        {
            return null;
        }

        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(appId)))
        {
            using (process)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        var name = FileVersionInfo.GetVersionInfo(path).ProductName;
                        return new DesktopAppInfo(string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) : name, path, appId, path + ",0");
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Elevated or exiting process: try the next one.
                }
            }
        }

        return null;
    }

    private static DesktopAppInfo? GetFromAppsFolder(string appId)
    {
        try
        {
            var shellItem = NativeMethods.SHCreateItemInKnownFolder(
                NativeMethods.FOLDERID_AppsFolder,
                NativeMethods.KF_FLAG_DONT_VERIFY,
                appId,
                typeof(IShellItem2).GUID);
            string displayName = shellItem.GetString(ref PropertyKeys.PKEY_ItemNameDisplay);
            string path = shellItem.GetString(ref PropertyKeys.PKEY_Link_TargetParsingPath);

            return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? new DesktopAppInfo(displayName, path, appId, path + ",0")
                : null;
        }
        catch (COMException ex) when ((uint)ex.ErrorCode == (uint)HRESULT.ERROR_NOT_FOUND)
        {
            return null;
        }
    }
}
