using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace NpuTools.Obsidian;

internal static class KeyChords
{
    internal static KeyChord OpenInObsidian { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.O);
    internal static KeyChord CopyUri { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.C);
    internal static KeyChord CopyMarkdownLink { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.C);
    internal static KeyChord Reveal { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.E);
    internal static KeyChord Pin { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.P);
    internal static KeyChord QuickAppend { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.A);
    internal static KeyChord Delete { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.Delete);
}
