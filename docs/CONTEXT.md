# Context

## What This Repo Is

This is a single monorepo for multiple PowerToys Command Palette extensions. The goal is to keep development standards, shared build configuration, documentation, and automation in one place while still letting each extension install and uninstall independently.

Independent installability comes from each extension project's Windows package identity, app extension registration, COM class GUID, and `CommandProvider.Id`. It does not require separate git repositories.

## Why The Monorepo Exists

The earlier layout used separate folders under `C:\Portable` for Action Center, Simple Analytics, and NPU tools. That made each extension easy to reason about locally, but it also created drift:

- Shared Command Palette conventions were duplicated.
- Build/package versions had to be kept in sync by hand.
- AI agents working in one repo could not see the standards or patterns from the others.
- Publishing to GitHub would require many small repos for one related extension family.

This monorepo fixes that by making one source of truth for conventions, package versions, CI, and docs while preserving separate extension projects.

## PowerToys Command Palette Extensions

PowerToys Command Palette extensions run as out-of-process COM servers. PowerToys:

- Launches the extension process via COM activation.
- Calls the extension's `IExtension.GetProvider(...)`.
- Renders top-level commands and dock bands.
- Owns dock placement, AppBar behavior, fullscreen auto-hide, DPI, monitor positioning, and host process lifetime.

Each extension process provides the commands, pages, services, and package metadata for one separately installable feature area.

## Current Extension Families

### Action Center

`src/ActionCenterExtension` provides a Quick Settings dock button. It uses `SendInput` to send Win+A and the dock toggle workaround for the Command Palette focus behavior.

### Simple Analytics

`src/SimpleAnalyticsExtension` provides compact dock/system analytics: battery, Wi-Fi/network, CPU, and settings-controlled visibility.

### NPU Tools

The NPU projects are the migration target for tools previously built elsewhere, including Raycast-style workflows:

- Awake is implemented and includes typed workflows, schedules, a status dashboard, Smart Awake parsing, and the `NpuAwakeKeeper` daemon.
- Organize, image tools, text tools, notes, and developer toolbox currently exist as packageable shell projects.

## Key Gotchas

### Build, Deploy, Reload

`dotnet build` does not register an extension with Windows. After changing an extension, build it, register its generated `AppxManifest.xml`, then run "Reload Command Palette extensions" in PowerToys.

### Separate Package Identity Matters

Every installable extension project must keep a unique MSIX identity, COM class GUID, app extension ID, and provider ID. Sharing a git repo is fine; sharing those IDs is not.

### Provider And Command IDs Are Required

Set a non-empty `CommandProvider.Id`. Set a non-empty `Id` on commands and page commands used by dock bands. Missing IDs can cause silent dock failures.

### Dock Band Toggle Behavior

When a dock button opens system UI, Command Palette takes focus before `Invoke()` runs. That can close the UI first, then the command re-opens it. Use the state-toggle pattern with an auto-reset timer instead of trying to detect Windows 11 system UI windows.

### WinRT Wrapper Lifetime

WinRT event sources must be retained as instance fields for the lifetime of the service/page that owns them. Unsubscribe events and cancel timers when deterministic disposal is available.
