# Bugs And Known Risks

This is the active issue ledger for the monorepo.

## Open

### BUG-007: InvalidCastException in RemoveBackground Gray8 compositing

Extension: NpuImageEditorExtension  
Severity: High — Remove Background operation always fails  
Discovered: 2026-05-18

`RemoveBackgroundAsync` throws `InvalidCastException: Invalid cast from 'WinRT.IInspectable' to 'NpuTools.ImageEditor.Interop.IMemoryBufferByteAccess'` when trying to lock the SoftwareBitmap buffers for Gray8 mask compositing.

The `IMemoryBufferByteAccess` COM interop interface is defined in `Interop/IMemoryBufferByteAccess.cs` with the standard `[ComImport]` / `[Guid]` / `[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]` pattern. The cast from the buffer reference to the interface is failing at runtime under the WinRT/CsWinRT projection used by the experimental7 SDK.

Likely cause: CsWinRT 2.x projects the `IMemoryBufferReference` to a WinRT-projected type that does not support direct `[ComImport]` interface casting. The raw COM query approach may need to be replaced with the CsWinRT-compatible `IMemoryBufferByteAccess` pattern (e.g., using `Marshal.GetComInterfaceForObject` / `Marshal.Release` or the CsWinRT `As<T>` interop helper).

Do not attempt to debug until after sleeping. See `Services/ImageEditorService.cs :: ApplyGray8MaskAsAlpha`.

### BUG-008: NpuOrganize screenshot renamer producing "screenshot" slug instead of AI description

Extension: NpuOrganizeExtension  
Severity: Medium — rename produces non-descriptive output, not broken  
Discovered: 2026-05-18

The OrganizeKeeper screenshot watcher is renaming new screenshots to a generic "screenshot" slug rather than running the `ImageDescriptionGenerator` AI pipeline to produce a meaningful slug. This regression was noticed during the image editor work session. The previous behavior was correct AI-generated slugs.

Possible causes: `ImageDescriptionGenerator.GetReadyState()` returning a non-ready state that is now silently falling back to a default slug; or the SDK upgrade to `2.0.0-experimental7` changed runtime behavior of `ImageDescriptionGenerator`; or a code path change introduced a fallback that short-circuits the AI call.

Do not debug now. See `NpuOrganizeKeeper/Watcher.cs` and `NpuOrganizeExtension/Services/AiNamingService.cs`.

## Resolved

### BUG-001: GetItems() blocking on async AI (fixed 2026-05)

Extensions: NpuTextToolsExtension, NpuImageEditorExtension

`RewriteResultPage` and `ImageResultPage` were calling `.GetAwaiter().GetResult()` on
AI service calls inside `GetItems()`. `GetItems()` runs on the COM apartment thread;
blocking it froze the entire Command Palette UI for the duration of the AI call
(typically 3–15 seconds for a vision or language model).

Fix: lazy async start on first `GetItems()` call via `Interlocked` flag. Page shows a
"Processing…" placeholder and calls `RaiseItemsChanged()` when the task completes.
See `CONVENTIONS.md § SDK Async Rules` for the canonical pattern.

### BUG-002: FallbackCommands() returning empty array (fixed 2026-05)

Extension: MediaControlsExtension

`_fallbackCommands` was initialized to `[]` (empty array) before fallbacks loaded.
SDK contract: `FallbackCommands()` must return `null` until actual fallbacks are ready.
Returning `[]` signals "fallback system exists, no items" rather than "no fallbacks".

Fix: field initialized to `null` (implicit default); populated and `RaiseItemsChanged()`
called once `InitializeFallbackCommandsAsync` completes.

### BUG-003: new IconInfo() inside GetItems() and timer callbacks (fixed 2026-05)

Extensions: ActionCenterExtension, SimpleAnalyticsExtension

`ActionCenterExtensionPage.GetItems()` created `new IconInfo("")` on every call.
The Simple Analytics dock refresh path created new `IconInfo` objects on every
timer tick (every 15–30 s). Icons are static values that should be allocated once.

Fix: `static readonly IconInfo` fields for single icons; `static readonly IconInfo[]`
lookup tables populated in a `static` constructor for ranged battery/WiFi codepoints.

### BUG-004: ContentPage missing RaiseItemsChanged on settings change (fixed 2026-05)

Extension: ActionCenterExtension

`SettingsPage` (a `ContentPage`) did not subscribe to `SettingsChanged` and never
called `RaiseItemsChanged()`. The SDK only re-calls `GetContent()` when told content
changed. Without the subscription, reopening the settings page after a change could
show stale content.

Fix: constructor subscribes to `_settingsManager.Settings.SettingsChanged` and calls
`RaiseItemsChanged()` in the handler. `#pragma warning disable CA1001` added per the
process-lifetime page convention.

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

### BUG-006: Media Controls package identity colliding with upstream package (fixed 2026-05)

Extension: MediaControlsExtension

The local media controls port used the generic MSIX identity `MediaControlsExtension`.
On machines that also had another user's unpackaged copy registered under the same
identity, `Add-AppxPackage -Register` failed with `0x80073D19` before the current
user could deploy local builds.

Fix: changed the package identity to `Dziad.MediaControlsExtension` while preserving
the extension provider ID and command IDs.

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
