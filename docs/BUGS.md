# Bugs And Known Risks

This is the active issue ledger for the monorepo.

## Open

### BUG-008: NpuOrganize screenshot renamer producing "screenshot" slug instead of AI description

Extension: NpuOrganizeExtension  
Severity: Medium — rename produces non-descriptive output, not broken  
Discovered: 2026-05-18

The OrganizeKeeper screenshot watcher is renaming new screenshots to a generic "screenshot" slug rather than running the `ImageDescriptionGenerator` AI pipeline to produce a meaningful slug. This regression was noticed during the image editor work session.

Possible causes: `ImageDescriptionGenerator.GetReadyState()` returning a non-ready state that is now silently falling back to a default slug; or a code path change introduced a fallback that short-circuits the AI call.

Do not debug now. See `NpuOrganizeKeeper/Watcher.cs` and `NpuOrganizeExtension/Services/AiNamingService.cs`.

---

## Resolved

### ~~BUG-009: Duplicate primary action in ClipboardHistoryPage MoreCommands flyout~~ — RESOLVED 2026-05-18

Extension: NpuClipboardExtension  
Severity: Low — cosmetic, functionally harmless  
Discovered: 2026-05-18 / Fixed: 2026-05-18

The MoreCommands flyout for each clipboard history entry showed the primary action (Copy or Paste) twice: once auto-inserted by the SDK at the top of the flyout (activated by Enter, no shortcut label), and again as an explicit `CommandContextItem` with its keyboard shortcut.

Root cause: The SDK always displays the `ListItem`'s primary command as the first entry in the MoreCommands flyout automatically. The original `ClipboardHistoryPage` code added both `CopyEntryCommand` and `PasteEntryCommand` to `MoreCommands` unconditionally, causing a duplicate for whichever one matched the user's `PrimaryAction` setting.

Fix: `BuildMoreCommands` now reads `settings.PrimaryAction` and only adds the *alternate* action (if primary is Paste → add Copy with Ctrl+C; if primary is Copy → add Paste with Ctrl+V). The plain-text variants (Ctrl+Shift+V, Ctrl+Shift+C) always appear since they are distinct from both primary options.

Convention updated: see `CONVENTIONS.md § MoreCommands and the SDK Primary Action`.

### ~~BUG-007: InvalidCastException in RemoveBackground Gray8 compositing~~ — RESOLVED 2026-05-18

Extension: NpuImageEditorExtension  
Severity: High — Remove Background operation always fails  
Discovered: 2026-05-18 / Fixed: 2026-05-18

Root cause: CsWinRT projects `IMemoryBufferReference` as a managed `WinRT.IInspectable` wrapper. Direct C# casts to a custom `[ComImport]` / `IUnknown`-based `IMemoryBufferByteAccess` interface always throw `InvalidCastException` — the managed wrapper does not surface IUnknown QI through the normal C# cast path.

Fix: replaced the raw unsafe-pointer compositing in `ApplyGray8MaskAsAlpha` with `SoftwareBitmap.CopyToBuffer` / `CopyFromBuffer` using `byte[].AsBuffer()` (from `System.Runtime.InteropServices.WindowsRuntime`). The `Interop/IMemoryBufferByteAccess.cs` file and the `Interop/` folder were deleted — they are no longer needed.

### ~~BUG-006: Media Controls package identity mismatch with deploy script~~ — RESOLVED 2026-05-18

Extension: MediaControlsExtension  
Severity: High — re-registration always fails with 0x80073CFB  
Discovered: 2026-05-18 / Fixed: 2026-05-18

A previous fix (see history) renamed the package identity to `Dziad.MediaControlsExtension` to avoid an upstream collision. However `Refresh-ExtensionRegistrations.ps1` searches for `"MediaControlsExtension"` (no prefix) in `$packageNames`. The mismatch caused the old `Dziad.MediaControlsExtension` package to never be unregistered, and the new registration to fail with `0x80073CFB` ("package already installed, reinstallation blocked at same version").

Fix: removed the orphaned `Dziad.MediaControlsExtension_0.10.0.1` package manually, then reverted `Package.appxmanifest` to `Name="MediaControlsExtension"` and `Version="0.0.1.0"` to match the monorepo convention and the deploy script. All 11 extensions now register cleanly.

### BUG-005: Blocking AI call and silent exception swallow in rename (fixed 2026-05)

Extension: NpuOrganizeExtension

`RenameAllCommand.Invoke()` called `AiNamingService.BuildProposedPath()` synchronously
(which itself called `DescribeAsync().GetAwaiter().GetResult()`) inside a loop over all
proposals. For a batch of 10 files this could block the COM thread for 1–2 minutes.
`catch { failed++; }` swallowed all exceptions without logging.

Fix:
- `AiNamingService` gains `BuildProposedPathAsync` / `GenerateSlugAsync` using proper
  `await`. Sync overload retained for callers that don't need AI.
- `RenameAllCommand` replaced by `RenameAllPage` (a `ListPage`). User navigates into
  it to confirm the rename; the async loop starts on first `GetItems()` call.
  Exceptions are logged with `Debug.WriteLine` including type and path.
- `RenameSingleCommand` updated to fire-and-forget with `Task.Run`.

### BUG-004: ContentPage missing RaiseItemsChanged on settings change (fixed 2026-05)

Extension: ActionCenterExtension

`SettingsPage` (a `ContentPage`) did not subscribe to `SettingsChanged` and never
called `RaiseItemsChanged()`. The SDK only re-calls `GetContent()` when told content
changed. Without the subscription, reopening the settings page after a change could
show stale content.

Fix: constructor subscribes to `_settingsManager.Settings.SettingsChanged` and calls
`RaiseItemsChanged()` in the handler. `#pragma warning disable CA1001` added per the
process-lifetime page convention.

### BUG-003: new IconInfo() inside GetItems() and timer callbacks (fixed 2026-05)

Extensions: ActionCenterExtension, SimpleAnalyticsExtension

`ActionCenterExtensionPage.GetItems()` created `new IconInfo("")` on every call.
The Simple Analytics dock refresh path created new `IconInfo` objects on every
timer tick (every 15–30 s). Icons are static values that should be allocated once.

Fix: `static readonly IconInfo` fields for single icons; `static readonly IconInfo[]`
lookup tables populated in a `static` constructor for ranged battery/WiFi codepoints.

### BUG-002: FallbackCommands() returning empty array (fixed 2026-05)

Extension: MediaControlsExtension

`_fallbackCommands` was initialized to `[]` (empty array) before fallbacks loaded.
SDK contract: `FallbackCommands()` must return `null` until actual fallbacks are ready.
Returning `[]` signals "fallback system exists, no items" rather than "no fallbacks".

Fix: field initialized to `null` (implicit default); populated and `RaiseItemsChanged()`
called once `InitializeFallbackCommandsAsync` completes.

### BUG-001: GetItems() blocking on async AI (fixed 2026-05)

Extensions: NpuTextToolsExtension, NpuImageEditorExtension

`RewriteResultPage` and `ImageResultPage` were calling `.GetAwaiter().GetResult()` on
AI service calls inside `GetItems()`. `GetItems()` runs on the COM apartment thread;
blocking it froze the entire Command Palette UI for the duration of the AI call
(typically 3–15 seconds for a vision or language model).

Fix: lazy async start on first `GetItems()` call via `Interlocked` flag. Page shows a
"Processing…" placeholder and calls `RaiseItemsChanged()` when the task completes.
See `CONVENTIONS.md § SDK Async Rules` for the canonical pattern.

---

## Known Risks

### RISK-001: WinRT Wrapper Lifetime

Priority: High
Area: WinRT lifetime

WinRT wrappers can cause `AccessViolationException` in `WinRT.IObjectReference.Finalize` if they are garbage-collected while OS-side objects are still active.

Mitigation: retain WinRT source objects as instance fields for process lifetime. Unsubscribe events and cancel timers when deterministic disposal is available.

### RISK-002: Build vs Deploy Confusion

Priority: Medium
Area: Developer workflow

`dotnet build` does not register or reload an extension.

Mitigation: use the full build, `Add-AppxPackage -Register`, and Command Palette reload loop in `RUNBOOK.md`.

### RISK-003: Identity Collisions

Priority: Medium
Area: Packaging

Multiple extension projects in one repo can accidentally share provider IDs, COM GUIDs, or MSIX identities.

Mitigation: every installable extension keeps unique IDs. Review `Package.appxmanifest`, `[ExtensionName].cs`, and `CommandProvider.Id` when adding or copying projects.

### RISK-004: Stale Dock Pins

Priority: Medium
Area: Dock integration

Renaming or removing dock band IDs can leave stale pinned entries.

Mitigation: remove stale dock pins manually, reload Command Palette extensions, and keep dock command IDs stable once released.

### RISK-005: Git History Import Clarity

Priority: Low
Area: Repository migration

Old repos may have history that does not line up perfectly with the new `src/` paths.

Mitigation: merge old histories into the monorepo so commits remain reachable, and document any imported histories in `RUNBOOK.md` or migration commits.

### RISK-006: COM Thread Starvation From Blocking Async Calls

Priority: High
Area: SDK correctness

`GetItems()` and `Invoke()` run on the COM apartment thread. Any `.GetAwaiter().GetResult()` or `Task.Wait()` on the COM thread deadlocks or stalls the palette. This is especially dangerous with AI services (`LanguageModel`, `ImageDescriptionGenerator`) that can take 3–15 seconds.

Mitigation: enforce the async patterns in `CONVENTIONS.md § SDK Async Rules`. Never call `.GetAwaiter().GetResult()` from `GetItems()`, `UpdateSearchText()`, or `Invoke()`. AI service calls must always be awaited from a `Task.Run` background task.
