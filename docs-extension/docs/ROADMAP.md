# Roadmap

The goal is to add dock bands that the PowerToys dock does not already provide natively. Clock, battery, and network are built into the dock platform and are not ported. Features that the dock platform handles automatically (AppBar, fullscreen, DPI) are also retired.

## Milestone 1: Scaffolding And Quick Settings Band

Status: Not started

Goal: get the scaffold deployed and implement the primary feature that motivated the migration.

Plan:

1. Verify the scaffold builds and deploys cleanly (Build > Deploy in Visual Studio).
2. Confirm the extension appears in Command Palette after Reload.
3. Add `ICommandProvider3` (`GetDockBands`) to `ActionCenterExtensionCommandsProvider`.
4. Set a non-empty `Id` on the `CommandProvider`.
5. Implement `QuickSettingsCommand` — an `IInvokableCommand` that sends `Win+A` via `SendInput`.
6. Wire it as a dock band and verify it appears in the dock and works.

Verification:

- Extension appears in palette search.
- Dock band appears in the dock after enabling it.
- Clicking the button opens or toggles Windows Quick Settings.
- No crash on repeated use.

## Deferred / Investigate

- **Eye Break IPC**: Port the named-pipe client if the external eye-break service is still in use. Implement as a `EyeBreakBand` with visual state driven by IPC messages.
- **Volume scroll**: The dock API may not expose scroll events on bands. Investigate whether `PointerWheelChanged` or equivalent is available. If not, skip.
- **Media controls**: Possible as an SMTC-based dock band. Defer until Quick Settings is stable.
- **Virtual desktop indicator**: Requires COM interop with the virtual desktop API. Complex and lower priority.
- **Active window title**: The dock is not designed for this — skip.

## Retired (Handled By Dock Platform)

- Clock — built into the PowerToys dock natively.
- Battery — built into the PowerToys dock natively.
- Network — built into the PowerToys dock natively.
