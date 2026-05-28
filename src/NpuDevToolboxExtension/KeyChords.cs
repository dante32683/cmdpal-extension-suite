using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace NpuTools.DevToolbox;

internal static class KeyChords
{
    internal static KeyChord Explorer    { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.E);
    internal static KeyChord Terminal    { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.T);
    internal static KeyChord Ide         { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.I);
    internal static KeyChord CopyPath    { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.C);
    internal static KeyChord RemoveRecent   { get; } = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: (int)VirtualKey.Delete);
    internal static KeyChord CommitMessage  { get; } = KeyChordHelpers.FromModifiers(ctrl: true, vkey: (int)VirtualKey.G);
}
