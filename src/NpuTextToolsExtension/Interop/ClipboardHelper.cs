using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NpuTools.TextTools.Interop;

internal static partial class ClipboardHelper
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr hWndNewOwner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetClipboardData(uint uFormat);

    [LibraryImport("user32.dll")]
    private static partial uint EnumClipboardFormats(uint format);

    [LibraryImport("kernel32.dll")]
    private static partial UIntPtr GlobalSize(IntPtr hMem);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalFree(IntPtr hMem);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    internal static partial uint GetClipboardSequenceNumber();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalLock(IntPtr hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr hMem);

    internal static bool SetText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            if (!EmptyClipboard()) return false;
            byte[] bytes = Encoding.Unicode.GetBytes(text + '\0');
            var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)bytes.Length);
            if (hMem == IntPtr.Zero) return false;
            var ptr = GlobalLock(hMem);
            if (ptr == IntPtr.Zero)
            {
                GlobalFree(hMem);
                return false;
            }

            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            GlobalUnlock(hMem);
            if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
            {
                GlobalFree(hMem);
                return false;
            }
            // Ownership transfers to the clipboard after SetClipboardData succeeds.
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    internal sealed class ClipboardSnapshot
    {
        internal List<(uint Format, byte[] Data)> Formats { get; } = [];
    }

    internal static ClipboardSnapshot? CaptureSnapshot()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var snapshot = new ClipboardSnapshot();
            uint format = 0;
            while ((format = EnumClipboardFormats(format)) != 0)
            {
                IntPtr handle = GetClipboardData(format);
                UIntPtr size = handle == IntPtr.Zero ? UIntPtr.Zero : GlobalSize(handle);
                if (handle == IntPtr.Zero || size == UIntPtr.Zero || size.ToUInt64() > int.MaxValue)
                    continue;

                IntPtr ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero) continue;
                try
                {
                    byte[] data = new byte[(int)size.ToUInt64()];
                    Marshal.Copy(ptr, data, 0, data.Length);
                    snapshot.Formats.Add((format, data));
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            return snapshot;
        }
        finally
        {
            CloseClipboard();
        }
    }

    internal static bool RestoreSnapshot(ClipboardSnapshot snapshot)
    {
        if (!OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            if (!EmptyClipboard()) return false;
            foreach (var (format, data) in snapshot.Formats)
            {
                IntPtr handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(uint)data.Length);
                if (handle == IntPtr.Zero) return false;
                IntPtr ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero)
                {
                    GlobalFree(handle);
                    return false;
                }
                Marshal.Copy(data, 0, ptr, data.Length);
                GlobalUnlock(handle);
                if (SetClipboardData(format, handle) == IntPtr.Zero)
                {
                    GlobalFree(handle);
                    return false;
                }
            }
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    internal static string? GetText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var hData = GetClipboardData(CF_UNICODETEXT);
            if (hData == IntPtr.Zero) return null;
            var ptr = GlobalLock(hData);
            if (ptr == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                GlobalUnlock(hData);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }
}
