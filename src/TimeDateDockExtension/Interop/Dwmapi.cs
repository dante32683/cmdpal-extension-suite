using System.Runtime.InteropServices;

namespace TimeDateDockExtension.Interop;

internal static partial class Dwmapi
{
    internal const int DWMWA_CLOAKED = 14;

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
}
