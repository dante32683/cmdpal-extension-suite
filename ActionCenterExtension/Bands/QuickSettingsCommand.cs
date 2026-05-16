using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ActionCenterExtension.Interop;
using ActionCenterExtension;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ActionCenterExtension.Bands;

internal sealed partial class QuickSettingsCommand : InvokableCommand
{
    private readonly SettingsManager _settings;
    private bool _isOpen;
    private CancellationTokenSource? _resetCts;

    public QuickSettingsCommand(SettingsManager settings)
    {
        _settings = settings;
        Id = "com.dziad.actioncenterextension.quicksettings";
        Name = "Quick Settings";
        Icon = new IconInfo(""); // Segoe MDL2 settings gear
    }

    public override CommandResult Invoke()
    {
        if (_isOpen)
        {
            CancelReset();
            _isOpen = false;
        }
        else
        {
            SendWinA();
            _isOpen = true;
            ScheduleReset();
        }
        return CommandResult.KeepOpen();
    }

    private void ScheduleReset()
    {
        CancelReset();
        var cooldown = _settings.QuickSettingsCooldown;
        if (cooldown is null)
            return; // "Never" — no auto-reset

        var cts = new CancellationTokenSource();
        _resetCts = cts;
        _ = Task.Delay(cooldown.Value, cts.Token)
              .ContinueWith(t => { if (!t.IsCanceled) _isOpen = false; });
    }

    private void CancelReset()
    {
        _resetCts?.Cancel();
        _resetCts = null;
    }

    private static void SendWinA()
    {
        User32.INPUT[] inputs =
        [
            new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_LWIN } } },
            new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_A } } },
            new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_A, dwFlags = User32.KEYEVENTF_KEYUP } } },
            new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_LWIN, dwFlags = User32.KEYEVENTF_KEYUP } } },
        ];
        User32.SendInput((uint)inputs.Length, ref inputs[0], Marshal.SizeOf<User32.INPUT>());
    }
}
