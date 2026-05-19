using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace NpuTools.Organize;

internal static class KeyChords
{
    internal static KeyChord CopyImage { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.C);
    internal static KeyChord CopyPath  { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.C);
    internal static KeyChord Reveal    { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.E);
}
