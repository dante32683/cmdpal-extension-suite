using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace NpuTools.Clipboard;

internal static class KeyChords
{
    internal static KeyChord Copy          { get; } = KeyChordHelpers.FromModifiers(ctrl: true,  vkey: (int)VirtualKey.C);
    internal static KeyChord Paste         { get; } = KeyChordHelpers.FromModifiers(ctrl: true,  vkey: (int)VirtualKey.V);
    internal static KeyChord PastePlain    { get; } = KeyChordHelpers.FromModifiers(ctrl: true,  shift: true, vkey: (int)VirtualKey.V);
    internal static KeyChord CopyPlain     { get; } = KeyChordHelpers.FromModifiers(ctrl: true,  shift: true, vkey: (int)VirtualKey.C);
    internal static KeyChord Rename        { get; } = KeyChordHelpers.FromModifiers(vkey: (int)VirtualKey.F2);
    internal static KeyChord Pin           { get; } = WellKnownKeyChords.TogglePin;
    internal static KeyChord Delete        { get; } = KeyChordHelpers.FromModifiers(ctrl: true,  shift: true, vkey: (int)VirtualKey.Delete);
}
