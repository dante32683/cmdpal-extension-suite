using NpuTools.Clipboard.Interop;

namespace NpuTools.Clipboard.Services;

internal static class PasteInputSequence
{
    // PowerToys' keyboard hook recognizes this marker and lets synthesized input pass through.
    // This is the same marker used by the built-in Command Palette clipboard extension.
    internal const nuint PowerToysInjectedInputMarker = 0x5555;

    internal static User32.INPUT[] Create() =>
    [
        Key(User32.VK_LCONTROL, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_RCONTROL, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_LWIN, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_RWIN, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_LSHIFT, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_RSHIFT, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_LMENU, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_RMENU, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_CONTROL, 0),
        Key(User32.VK_V, 0),
        Key(User32.VK_V, User32.KEYEVENTF_KEYUP),
        Key(User32.VK_CONTROL, User32.KEYEVENTF_KEYUP),
    ];

    private static User32.INPUT Key(ushort virtualKey, uint flags) => new()
    {
        type = User32.INPUT_KEYBOARD,
        U = new User32.INPUTUNION
        {
            ki = new User32.KEYBDINPUT
            {
                wVk = virtualKey,
                dwFlags = flags,
                dwExtraInfo = PowerToysInjectedInputMarker,
            },
        },
    };
}
