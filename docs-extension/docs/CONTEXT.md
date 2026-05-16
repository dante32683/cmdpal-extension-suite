# Context And Migration Background

## What This Repo Is

This is a PowerToys Command Palette extension that provides a persistent dock bar — a row of status widgets and action buttons pinned to the top of the screen. It is the native successor to a hand-rolled WinUI 3 menubar app.

## Where It Came From

The previous app (`C:\Portable\MenuBar\winui3`) was a standalone WinUI 3 desktop app that manually:

- Registered an AppBar window with `SHAppBarMessage` to dock at the top of the screen and reserve screen space.
- Managed its own fullscreen detection via WinEvent hooks (`EVENT_SYSTEM_FOREGROUND`) and a 1-second poll fallback, because browsers and some apps do not fire the foreground event when going fullscreen in-place.
- Managed Explorer restart recovery (reregistering the AppBar after Explorer crashes/restarts).
- Implemented all DPI and multi-monitor positioning logic by hand.
- Implemented its own tooltip system using a custom `Popup` because WinUI's `ToolTipService` is unreliable on a no-activate `WS_EX_TOOLWINDOW` AppBar window.
- Implemented all service and lifetime management for WinRT wrappers (battery, network, SMTC), with retention workarounds to prevent idle crashes from `WinRT.IObjectReference.Finalize`.

That app worked, but it required ongoing investment to keep stable. It had a multi-milestone stabilization roadmap (observability, lifetime hardening, MainWindow decomposition, service boundaries, idle performance) and still had active bugs.

## Why We Moved

PowerToys v0.98.0 (released March 17, 2026) shipped the Command Palette Dock — a persistent toolbar at the screen edge that is built into PowerToys and managed entirely by the PowerToys runtime. Extensions provide dock bands; PowerToys handles:

- AppBar registration and screen space reservation.
- Fullscreen auto-hide.
- Explorer restart recovery.
- DPI and multi-monitor positioning.
- z-order management.
- The host process lifetime.

This eliminates the entire infrastructure layer that the old app had to maintain. Instead of shipping a full app, we ship a small COM server that provides widgets. The framework is stable, already shipped, and maintained by Microsoft.

The primary feature that motivated moving immediately was the Quick Settings button — a single button that sends `Win+A` to toggle the Windows Quick Settings panel. That's one `IInvokableCommand` in a dock band. It took more code to maintain the surrounding framework than the feature itself.

## What The Old App Had

For reference, here is a full feature list of the WinUI 3 app, with migration status:

| Feature | Old app | Dock migration |
|---|---|---|
| AppBar docking + screen reservation | Manual SHAppBarMessage | PowerToys handles it — nothing to port |
| Fullscreen auto-hide | Manual WinEvent + 1s poll | PowerToys handles it — nothing to port |
| Explorer restart recovery | Manual TaskbarCreated hook | PowerToys handles it — nothing to port |
| DPI / multi-monitor | Manual WM_DPICHANGED handling | PowerToys handles it — nothing to port |
| Quick Settings button (Win+A) | QuickSettingsController + suppression logic | Port as dock band IInvokableCommand |
| Clock | DispatcherTimer, 1s tick | Built into PowerToys dock natively — retired |
| Battery | BatteryService (WinRT), BatteryPresenter | Built into PowerToys dock natively — retired |
| Network | NetworkService (WinRT) | Built into PowerToys dock natively — retired |
| Volume scroll | Scroll event on bar segment | May not be supported by dock API — investigate |
| Active window title + icon | WinEvent hook, process scan | Dock is not designed for this — skip or defer |
| Virtual desktop indicator | VirtualDesktopService, COM interop | Defer — complex COM interop, low priority |
| App menu extraction | UI Automation, HMENU | Skip — not a good fit for a dock widget |
| Media controls | SMTC integration, MediaPresenter | Defer — possible as a dock band |
| Eye Break IPC | Named pipe client to external process | Port if eye-break service is still in use |
| Tooltip system | Custom Popup with cursor probe | Dock SDK handles tooltips natively |
| Settings | settings.json, SettingsService | Port using extension settings API |

## What The Scaffold Provides

The scaffold at `C:\Portable\ActionCenterExtension` was generated with `create extension` and provides:

- `Program.cs` — COM server registration and entry point. Do not modify the hosting pattern.
- `ActionCenterExtension.cs` — `IExtension` implementation with GUID `95b85877-41fd-45b1-811f-cd6ac83f3057`. Returns the `CommandProvider` to the palette host.
- `ActionCenterExtensionCommandsProvider.cs` — `CommandProvider` subclass. This is the main entry point. Override `TopLevelCommands()` for palette commands and `GetDockBands()` for dock widgets.
- `Pages/ActionCenterExtensionPage.cs` — Stub `ListPage`. Replace or extend with real pages.
- `.github/skills/` — Copilot skill files for common patterns (add-dock-band, add-extension-settings, etc.). These are useful references even when not using Copilot.

The scaffold is a working COM server. Build and deploy it once to verify the plumbing before adding any features.

## Key Gotchas From Research

- **Deploy, not just Build.** After changes, use Visual Studio's Build > Deploy (or the equivalent publish step) to register the MSIX package. A plain `dotnet build` does not update the registered extension.
- **Reload after deploy.** In Command Palette, run the `Reload` command → "Reload Command Palette extensions". Changes are not picked up automatically.
- **Dock bands need non-empty IDs.** Every `ICommandItem` returned from `GetDockBands()` must have a `Command` with a non-empty `Id`. Items without IDs are silently ignored by the dock.
- **Set `Id` on the `CommandProvider` too.** The dock uses the provider `Id` for pinning and persistence. Set it to something like `"com.dziad.actioncenterextension"`.
- **Palette dismisses before command executes.** When a user activates a command, the palette closes first. If sending a keypress (like Win+A), this is actually helpful — the shell regains focus before the key is sent.
- **SDK version must be 0.9+.** Dock support (`GetDockBands`) requires `Microsoft.CommandPalette.Extensions` ≥ 0.9.260303001. Verify in `Directory.Packages.props`.

## What To Do First

1. Read `ARCHITECTURE.md` to understand the SDK model.
2. Read `RUNBOOK.md` to get the build/deploy/reload cycle working.
3. Implement the Quick Settings dock band — a single `IInvokableCommand` that calls `SendInput(VK_LWIN, 'A')`. This is the first real feature and the proof-of-concept for the whole migration.
4. Add dock band support (`ICommandProvider3`) to the `CommandProvider`. The `.github/skills/add-dock-band/SKILL.md` file has working code examples.
