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

### Dock Extensions

- **ActionCenterExtension** — Quick Settings dock button. Uses `SendInput` (Win+A) and the state-toggle workaround for Command Palette focus behavior.
- **TimeDateDockExtension** — Individually addable time and date dock buttons. Opens Notification Center on click.
- **MediaControlsExtension** — Media playback dock controls with now-playing info, volume, and per-session switching.
- **SimpleAnalyticsExtension** — Battery, Wi-Fi/network, and CPU dock/status analytics with settings-controlled visibility.

### NPU Tools (implemented)

- **NpuAwakeExtension** — Typed workflows, schedules, Smart Awake parsing, status dashboard, `NpuAwakeKeeper` daemon.
- **NpuOrganizeExtension** — AI screenshot rename (`ImageDescriptionGenerator` → slug), content-indexed search, `NpuOrganizeKeeper` watcher daemon.
- **NpuImageEditorExtension** — OCR (`OcrEngine`), background removal (`ImageObjectExtractor`), 2× upscale (`ImageScaler`).
- **NpuTextToolsExtension** — Six AI rewrite modes via Phi `LanguageModel` (grammar, formal, concise, bullets, simplify, custom).
- **NpuClipboardExtension** — Clipboard history recorder, search, filter by type, copy/paste/pin/delete/rename, `NpuClipboardKeeper` daemon.

### Shell Projects (not yet implemented)

- **NpuDevToolboxExtension**

## Key Gotchas

### GetItems() and Invoke() Must Not Block

`GetItems()` is called on the COM apartment thread. Blocking it — including with `.GetAwaiter().GetResult()` on async AI operations — freezes the entire Command Palette UI for the duration of the block. For AI model calls this is 3–15 seconds of a completely unresponsive palette.

Use the lazy async pattern: fire `Task.Run` on first `GetItems()` call via `Interlocked.Exchange`, return a placeholder immediately, call `RaiseItemsChanged()` when the task completes. See `CONVENTIONS.md § SDK Async Rules` for the full pattern with code.

`Invoke()` has the same constraint for long-running work: use fire-and-forget with `Task.Run` or convert the command to a `ListPage` (the Result Page Pattern) when results need to be displayed.

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
