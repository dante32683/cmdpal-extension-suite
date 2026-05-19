using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace NpuTools.ImageEditor;

internal static class KeyChords
{
    internal static KeyChord Reveal   { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.E);
    internal static KeyChord CopyPath { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.C);
    internal static KeyChord CopyText { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.C);
}
