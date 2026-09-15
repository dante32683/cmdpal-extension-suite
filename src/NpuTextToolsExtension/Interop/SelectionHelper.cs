using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NpuTools.TextTools.Interop;

// Captures text from the active application by simulating Ctrl+C and reading the clipboard.
internal static partial class SelectionHelper
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_C = 0x43;

    // INPUT struct for KEYBDINPUT variant; Size=40 matches the x64 Win32 layout of INPUT.
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort VKey;
        [FieldOffset(10)] public ushort Scan;
        [FieldOffset(12)] public uint KFlags;
        [FieldOffset(16)] public uint KTime;
        [FieldOffset(24)] public nint ExtraInfo;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    private static void SendCtrlC()
    {
        INPUT[] inputs =
        [
            new() { Type = INPUT_KEYBOARD, VKey = VK_CONTROL },
            new() { Type = INPUT_KEYBOARD, VKey = VK_C },
            new() { Type = INPUT_KEYBOARD, VKey = VK_C,       KFlags = KEYEVENTF_KEYUP },
            new() { Type = INPUT_KEYBOARD, VKey = VK_CONTROL, KFlags = KEYEVENTF_KEYUP },
        ];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    // Waits for the previous app to regain focus, sends Ctrl+C, then polls the clipboard
    // until the sequence number changes (indicating a copy occurred) or timeout elapses.
    // Returns the captured text, or null if nothing was captured.
    internal static async Task<string?> CaptureSelectionAsync(int initialDelayMs = 200, int timeoutMs = 800)
    {
        await Task.Delay(initialDelayMs);

        var snapshot = ClipboardHelper.CaptureSnapshot();
        uint seqBefore = ClipboardHelper.GetClipboardSequenceNumber();
        SendCtrlC();

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(40);
            uint sequence = ClipboardHelper.GetClipboardSequenceNumber();
            if (sequence != seqBefore)
            {
                string? result = ClipboardHelper.GetText();
                // Restore only if no other clipboard owner changed the clipboard after Ctrl+C.
                if (snapshot is not null && ClipboardHelper.GetClipboardSequenceNumber() == sequence)
                    ClipboardHelper.RestoreSnapshot(snapshot);
                return result;
            }
        }

        if (snapshot is not null && ClipboardHelper.GetClipboardSequenceNumber() != seqBefore)
        {
            uint sequence = ClipboardHelper.GetClipboardSequenceNumber();
            if (ClipboardHelper.GetClipboardSequenceNumber() == sequence)
                ClipboardHelper.RestoreSnapshot(snapshot);
        }
        return null;
    }
}
