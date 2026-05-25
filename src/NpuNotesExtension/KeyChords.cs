using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace NpuTools.Notes;

internal static class KeyChords
{
    internal static KeyChord CopyContent { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.C);
    internal static KeyChord CopyPath { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.C);
    internal static KeyChord Reveal { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.E);
    internal static KeyChord Pin { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.P);
    internal static KeyChord Delete { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.Delete);
}
