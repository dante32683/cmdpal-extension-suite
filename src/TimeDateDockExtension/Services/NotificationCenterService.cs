using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TimeDateDockExtension.Interop;

namespace TimeDateDockExtension.Services;

internal sealed partial class NotificationCenterService : IDisposable
{
    private readonly object _lock = new();
    private bool _isOpen;
    private CancellationTokenSource? _resetCts;

    public void Toggle()
    {
        lock (_lock)
        {
            if (_isOpen || IsNotificationCenterForeground() || TryFindVisibleNotificationCenterWindow(out _))
            {
                _isOpen = false;
                return;
            }

            SendWinN();
            _isOpen = true;
            ScheduleReset();
        }
    }

    public void Dispose()
    {
        CancelReset();
    }

    private void ScheduleReset()
    {
        CancelReset();

        var cts = new CancellationTokenSource();
        _resetCts = cts;

        _ = Task.Delay(TimeSpan.FromSeconds(10), cts.Token).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                return;
            }

            lock (_lock)
            {
                _isOpen = false;
                _resetCts = null;
            }
        });
    }

    private void CancelReset()
    {
        _resetCts?.Cancel();
        _resetCts?.Dispose();
        _resetCts = null;
    }

    private static void SendWinN()
    {
        User32.INPUT[] inputs =
        [
            new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_LWIN } } },
            new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_N } } },
            new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_N, dwFlags = User32.KEYEVENTF_KEYUP } } },
            new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_LWIN, dwFlags = User32.KEYEVENTF_KEYUP } } },
        ];

        User32.SendInput((uint)inputs.Length, ref inputs[0], Marshal.SizeOf<User32.INPUT>());
    }

    private static bool IsNotificationCenterForeground()
    {
        var hwnd = User32.GetForegroundWindow();
        return IsNotificationCenterWindow(hwnd);
    }

    private static bool TryFindVisibleNotificationCenterWindow(out nint notificationCenterHwnd)
    {
        nint foundHwnd = nint.Zero;

        try
        {
            User32.EnumWindows((hwnd, _) =>
            {
                try
                {
                    if (!User32.IsWindowVisible(hwnd))
                    {
                        return true;
                    }

                    Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DWMWA_CLOAKED, out int cloaked, sizeof(int));
                    if (cloaked != 0)
                    {
                        return true;
                    }

                    if (!IsNotificationCenterWindow(hwnd))
                    {
                        return true;
                    }

                    foundHwnd = hwnd;
                    return false;
                }
                catch
                {
                    return true;
                }
            }, nint.Zero);
        }
        catch
        {
            foundHwnd = nint.Zero;
        }

        notificationCenterHwnd = foundHwnd;
        return notificationCenterHwnd != nint.Zero;
    }

    private static bool IsNotificationCenterWindow(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return false;
        }

        var root = User32.GetAncestor(hwnd, User32.GA_ROOT);
        if (root != nint.Zero)
        {
            hwnd = root;
        }

        var processName = GetProcessName(hwnd);
        var className = GetWindowClass(hwnd);
        var title = GetWindowTitle(hwnd);

        if (!processName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return className.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase)
            && title.Contains("Notification", StringComparison.OrdinalIgnoreCase);
    }

    private static unsafe string GetWindowTitle(nint hwnd)
    {
        char* title = stackalloc char[256];
        var length = User32.GetWindowText(hwnd, title, 256);
        return length > 0 ? new string(title, 0, length) : string.Empty;
    }

    private static unsafe string GetWindowClass(nint hwnd)
    {
        char* className = stackalloc char[256];
        var length = User32.GetClassName(hwnd, className, 256);
        return length > 0 ? new string(className, 0, length) : string.Empty;
    }

    private static string GetProcessName(nint hwnd)
    {
        if (hwnd == nint.Zero)
        {
            return string.Empty;
        }

        User32.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
