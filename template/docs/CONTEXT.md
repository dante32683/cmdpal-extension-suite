# Context

## What This Repo Is

This is a PowerToys Command Palette extension. Fill in what this extension does, what problem it solves, and why it was built.

## PowerToys Command Palette Extensions — Background

PowerToys v0.98.0 (released March 17, 2026) shipped the Command Palette, a launcher and command surface built into PowerToys. Extensions add:

- **Top-level commands** — items that appear in the palette home screen and search results.
- **Dock bands** — persistent widgets pinned to a toolbar at the screen edge (requires PowerToys v0.98+ and SDK ≥ 0.9.260303001).

The extension runs as an out-of-process COM server (a background process). PowerToys:
- Launches the extension process via COM activation.
- Handles the dock toolbar, AppBar docking, fullscreen auto-hide, DPI, and multi-monitor positioning.
- Manages the host process lifetime.

The extension only needs to provide the widget logic. All infrastructure is handled by PowerToys.

## What This Extension Does

TODO: describe the specific purpose of this extension.

## Key Gotchas (Learned From Experience)

These are non-obvious behaviors that have caused bugs in practice:

### Deploy, Not Just Build

A plain `dotnet build` does not register the extension. You must run `Add-AppxPackage -Register ...` after every build. See RUNBOOK.md.

### Reload After Deploy

After deploying, run "Reload Command Palette extensions" in the palette. Changes are not picked up automatically.

### Dock Band IDs Must Be Non-Empty

Every `ICommandItem` returned from `GetDockBands()` must have a `Command.Id` set to a non-empty string. Items without IDs are silently ignored — no error, no band.

### CommandProvider.Id Must Be Set

Set a non-empty `Id` on the `CommandProvider` (e.g., `"com.yourname.extensionname"`). Required for dock persistence and pinning.

### Dock Band Toggle Behavior

When a dock band button is clicked, the Command Palette steals focus before `Invoke()` runs. If the button was supposed to toggle a window (e.g., a flyout or system panel), that focus shift may close the window first — then `Invoke()` re-opens it. The result: clicking a second time re-opens instead of closing.

**Fix**: track open/closed state in a boolean field on the command. If `_isOpen`, skip the open action (focus loss already closed it) and flip the flag. If not `_isOpen`, perform the action and flip the flag. Add a timeout (e.g., 10 seconds via `CancellationTokenSource` + `Task.Delay`) to auto-reset the state flag in case the user closes the panel externally. See `QuickSettingsCommand.cs` in the ActionCenterExtension reference repo for a working example.

### Quick Settings Window Is Not Detectable via Win32

The Windows 11 Quick Settings panel (Win+A) does not create a standard top-level window. `FindWindow`, `EnumWindows`, `IsWindowVisible`, and `DwmGetWindowAttribute(DWMWA_CLOAKED)` all fail to reliably detect whether it is open. The window class `"Windows.UI.Core.CoreWindow"` with title `"Quick Settings"` does not exist on all Windows 11 builds. Use the state-toggle approach instead of window detection.

### Icon Glyphs Use Segoe Fluent Icons By Default

`new IconInfo("")` sets a glyph icon. The default font is **Segoe Fluent Icons**, not Segoe MDL2 Assets. Use explicit Unicode escapes (e.g., `""`) rather than pasting the glyph character directly into source — copy-paste can silently corrupt the character encoding. Verify the character with PowerShell: `[int][char]''` should return the expected codepoint.

### Duplicate Dock Band Warning

If the dock log shows "Skipping duplicate pinned dock band command", the dock has two pinned registrations for the same band ID. Right-click the dock band → Remove, then reload. The band re-registers cleanly on the next reload without needing to be manually re-added via the dock's Add menu.

### Do Not Modify Program.cs

`Program.cs` implements the COM server registration and lifetime loop. It must not be changed. The extension process is launched by PowerToys via `-RegisterProcessAsComServer`; do not try to run it manually in production.

### WinRT Wrapper Lifetime

WinRT event subscriptions (battery, network, SMTC) must be held as instance fields for the lifetime of the band. Short-lived WinRT wrappers that get GC'd while the OS-side object is still active cause `AccessViolationException` in `WinRT.IObjectReference.Finalize`. Unsubscribe all events in `Dispose`.
