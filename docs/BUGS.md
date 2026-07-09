# Bugs And Known Risks

This is the active issue ledger for the monorepo.

## Open

> Source: bug audit of the NpuClipboardExtension + NpuClipboardKeeper subsystem
> on 2026-07-09 (cross-device sync + secret-pattern filter code). All items below
> are unit-testable in `NpuTools.Tests` without the COM/MSIX host unless noted.
> Suggested fix order: BUG-017 → BUG-019 → BUG-021 → BUG-020 → (cleanups) →
> BUG-018 / BUG-022 on their own branches.

### BUG-017: `ClipboardSettingsStore.Load()` unconditionally `Save()`s — settings write storm and silent config/secret loss

Extension: NpuClipboardExtension / NpuClipboardKeeper
Severity: High — a just-saved secret pattern or sync folder can be silently and permanently lost; a dropped secret pattern means a secret that should have been filtered gets captured and synced
Discovered: 2026-07-09 (audit)
File: `src/NpuClipboardExtension/Shared/ClipboardSettingsStore.cs` (`Save()` call at the end of `Load()`)

Root cause:

`Load()` ends with `Normalize(...); Save();`, and `Reload()` is just `lock → Load()`,
so **every reload writes `settings.json` back to disk**. Two hot paths hit this
continuously:

- `NpuClipboardKeeper/Program.cs` calls `settings.Reload()` every 700 ms in the
  watch loop (~123K writes/day).
- `ClipboardHistoryPage.GetItems()` calls `_settings.Reload()` on every palette
  open / refresh, on the COM thread.
- `SecretPatternsPage.GetContent()` / `SubmitForm()` each construct a fresh
  `new ClipboardSettingsStore()` (another load+save per render).

Writes are plain temp-file + `File.Move` with no cross-process locking and no
merge, so any legitimate setting change that lands inside another process's
read→normalize→save window is clobbered — permanently, because the clobbering
process persists its stale in-memory copy. Example: user sets the sync folder
(toast confirms), but the keeper's in-flight `Save()` reverts it to null before
the next 700 ms reload picks it up.

Proposed fix (simple, safe):

- Remove `Save()` from `Load()`. Normalize in memory only.
- If default backfill must persist, do it once in the constructor and only when
  `Normalize` actually mutated something (track a `dirty` bool).
- Make `Update()` the sole write path.
- Makes BUG-018-on-settings and the unused-param half of the Low cleanups moot.

### BUG-018: `history.json` cross-process writes are last-writer-wins (TOCTOU)

Extension: NpuClipboardExtension / NpuClipboardKeeper
Severity: Medium — a keeper capture and a user action (pin/rename/delete/paste) landing together can drop one of the two
Discovered: 2026-07-09 (audit)
File: `src/NpuClipboardExtension/Shared/ClipboardStore.cs` (`Save` / `EnsureFresh`)

Root cause:

Both processes hold their own `ClipboardStore` guarded by an in-process `_lock`
only. `EnsureFresh()` reloads on mtime change, then the mutation `Save()`s. There
is a read-modify-write gap: process A `EnsureFresh()` (state X) → process B writes
X+edit → process A `Save()` writes X+otherEdit, dropping B's change. Rare
(sub-millisecond overlap) but real; inherent to the lock-free file design.

Proposed fix (moderate, own branch — deadlock risk if mis-scoped):

- Guard the read-modify-write in `Save`/`EnsureFresh` with a named cross-process
  `Mutex`, or retry-on-mtime-change. Not urgent; note as known risk if deferred.

### BUG-019: `SecretPatternMatcher.Match` fails **open** on regex timeout

Extension: NpuClipboardExtension
Severity: Medium — security filter fails in the unsafe direction: a secret that triggers regex backtracking is treated as "not a secret" and gets captured and synced to disk
Discovered: 2026-07-09 (audit)
File: `src/NpuClipboardExtension/Shared/SecretPatternMatcher.cs` (`Match`)

Root cause:

`Match()` catches `RegexMatchTimeoutException`, continues the loop, and ultimately
returns `null` (= "not a secret"). For a filter whose purpose is to keep secrets
off disk, a timeout should fail **closed** (drop the entry), not open.

Proposed fix (simple):

- On timeout, return a sentinel name (e.g. `"<pattern timeout>"`) so the caller
  drops the entry; log a warning. Trade-off: a slow benign pattern would drop
  legitimate text — acceptable given the security intent and the generous 100 ms
  timeout. Also audit `DefaultSecretPatterns` for ReDoS-prone rules.

### BUG-020: `ClipboardStore.DeleteOlderThan(window)` name is inverted vs. behavior

Extension: NpuClipboardExtension
Severity: Medium — latent data-loss trap; current callers are correct, a future caller will not be
Discovered: 2026-07-09 (audit)
File: `src/NpuClipboardExtension/Shared/ClipboardStore.cs`

Root cause:

`cutoff = Now - window; RemoveAll(e => e.CreatedAt >= cutoff)` removes entries
created *within* the last `window` (the recent ones). All current callers are
"Delete Last N Minutes" so behavior is correct today, but the method name means
the exact opposite. A future "delete entries older than 30 days" retention feature
calling `DeleteOlderThan(TimeSpan.FromDays(30))` would wipe the last 30 days.

Proposed fix (mechanical):

- Rename to `DeleteWithinLast(TimeSpan)` and update the call sites
  (`NpuClipboardCommandsProvider`, `ClipboardSettingsPage`). No behavior change.

### BUG-021: `SyncFrom` merges entries but never enforces retention

Extension: NpuClipboardExtension
Severity: Medium — a receive-mostly device can grow history past the configured limit until the next capture
Discovered: 2026-07-09 (audit)
File: `src/NpuClipboardExtension/Shared/ClipboardStore.cs` (`SyncFrom`)

Root cause:

`SyncFrom` inserts new synced entries and `Save()`s but never calls
`ApplyRetention`. Bounded and self-corrects at the next `AddOrPromote` /
`EnforceRetention`, but violates the retention contract in the meantime.

Proposed fix (one line):

- Call `ApplyRetention(settings.NormalizedRetentionLimit)` inside the
  `if (merged)` block before `Save()`.

### BUG-022: OCR text is never scanned against secret patterns

Extension: NpuClipboardKeeper
Severity: Low — a screenshot of an API key/password is OCR'd into `OcrText`, stored, and made search-indexed; the secret filter only runs on the `Text` branch. Lower risk (images are not synced cross-device) but the secret still lands on disk
Discovered: 2026-07-09 (audit)
File: `src/NpuClipboardKeeper/ClipboardCaptureService.cs` (Bitmap branch)

Proposed fix (own branch — touches the capture path, verify on a real screenshot
per AGENTS.md rule #5):

- Run `secretMatcher.Match(ocr)` on the image branch and drop or redact `OcrText`
  on match.

### Low-severity cleanups (batch alongside BUG-017..021)

- **Dead code:** `ClipboardStore.Groups()` has no callers in `src/` (the history
  page does its own grouping). Safe to delete.
- **Unused parameter:** `SecretPatternsPage(ClipboardSettingsStore settings)`
  ignores the injected store — `GetContent()` and `SubmitForm()` both construct a
  fresh `new ClipboardSettingsStore()`. Drop the parameter or use the injected one.
- **Cosmetic:** `ClipboardHistoryPage.BuildItems` assumes same-`GroupId` entries
  are contiguous, but `Search` orders by `IsPinned` then `LastUsedAt`; promoting
  one burst member (Copy/Paste bumps `LastUsedAt`) splits the group and emits its
  date header twice.
- **Edge case:** toggling the recorder off→on quickly can leave two
  `NpuClipboardKeeper` processes double-capturing (old instance still polls
  `stop.flag` every 700 ms). Consider a single-instance mutex in the keeper.

## Resolved

### ~~BUG-016: Clipboard Copy/Paste doesn't refresh the history list~~ — RESOLVED 2026-06-30

Extension: NpuClipboardExtension  
Severity: Medium — visible list stays stale after invoking Copy or Paste on an entry; user had to "Reload Command Palette extensions" to see the new LastUsedAt order  
Discovered: 2026-06-30 / Fixed: 2026-06-30

Root cause:

`CopyEntryCommand.Invoke()` and `PasteEntryCommand.Invoke()` mutate the store via
`MarkUsed` (updates `LastUsedAt`, moves the entry to position 0, calls `Save()`) but
never call `RaiseItemsChanged()` on the page. The SDK caches the result of
`GetItems()` and only re-fetches it when the page explicitly raises a change. The page
instance is reused across palette sessions, so the user saw the same list until the
COM process was restarted.

The same blind spot affected `PinEntryCommand`, `DeleteEntryCommand`, and external
keeper writes.

Fix:

- `ClipboardStore` gained `public event Action? Changed;` raised after every
  successful mutation (`AddOrPromote`, `MarkUsed`, `SetPinned`, `Delete`, `DeleteAll`,
  `DeleteOlderThan`, `Rename`, `EnforceRetention`, `SyncFrom`). The event is invoked
  outside `_lock` to avoid holding the lock across subscriber callbacks.
- Early-return paths in `MarkUsed`, `Rename`, `SetPinned`, and `DeleteOlderThan`
  suppress the event when the target id is missing or nothing changed, so empty
  no-op calls do not trigger a re-render.
- `ClipboardHistoryPage` and `AskClipboardPage` subscribe in their constructors and
  call `RaiseItemsChanged()` from the handler. The redundant manual
  `RaiseItemsChanged(0)` in the `SyncFrom` background block was removed; the store's
  event covers the merge case.
- `ClipboardStore.Save()` now calls `File.SetLastWriteTimeUtc(path, DateTime.UtcNow)`
  after the temp+move write to guarantee a distinct mtime. The test author had
  previously added an `AddSeconds(1)` workaround because `File.WriteAllText` +
  `File.Move` could land on the same NTFS mtime as the previous file, hiding the
  change from `EnsureFresh`'s `writeTime != _lastWriteTime` check.

Eleven new tests in `ClipboardStoreTests` cover the event firing / no-fire paths
and the mtime advancement.

### ~~BUG-015: DevToolbox WorkspaceScanner.Scan() called on COM thread~~ — RESOLVED 2026-05-27

Extension: NpuDevToolboxExtension  
Severity: High — freezes Command Palette UI while scanning filesystem on every keystroke  
Discovered: 2026-05-27 / Fixed: 2026-05-27

`DevToolboxHubPage.BuildItems()` and `QuickOpenCommand.BuildItems()` both called
`WorkspaceScanner.Scan([])` synchronously on every `UpdateSearchText` call. Scanning
~/source/repos and similar roots involves directory enumeration that can take 100–1000 ms
on machines with many directories, freezing the palette UI.

Fix: scan is started once with `Task.Run` in each page/command constructor. Result is
cached in a `volatile List<WorkspaceEntry> _scannedWorkspaces` field. `BuildItems()` reads
from the cache; initial render shows only recents while the scan runs in the background.
`RaiseItemsChanged` updates the display when the scan completes.

### ~~BUG-014: DevToolbox missing KeyChords.cs and RequestedShortcut on MoreCommands~~ — RESOLVED 2026-05-27

Extension: NpuDevToolboxExtension  
Severity: Medium — convention violation; users cannot use keyboard shortcuts on workspace actions  
Discovered: 2026-05-27 / Fixed: 2026-05-27

`DevToolboxHubPage.BuildWorkspaceItem()` created `CommandContextItem` entries in
`MoreCommands` with no `RequestedShortcut` set. `CONVENTIONS.md` requires a shortcut on
every primary-action `CommandContextItem`. No `KeyChords.cs` file existed.

Fix: added `KeyChords.cs` (Ctrl+E Explorer, Ctrl+T Terminal, Ctrl+I IDE, Ctrl+C CopyPath,
Ctrl+Shift+Delete RemoveRecent) and applied `RequestedShortcut` to all three MoreCommands
context items in `DevToolboxHubPage`.

### ~~BUG-013: DevToolbox wt.exe UseShellExecute=false bypasses OS PATH resolution~~ — RESOLVED 2026-05-27

Extension: NpuDevToolboxExtension  
Severity: Medium — Windows Terminal launch can silently fail on some configurations  
Discovered: 2026-05-27 / Fixed: 2026-05-27

`LaunchWindowsTerminal()` in `OpenInTerminalCommand` used `UseShellExecute = false`,
which requires `wt.exe` to be reachable through `Process.Start`'s own PATH lookup.
Windows Terminal is a Store app whose PATH registration can be unavailable in some
non-interactive process contexts (e.g. COM server launched without full user environment).

Fix: changed to `UseShellExecute = true` so ShellExecuteEx handles the path resolution,
consistent with all other IDE/app launches.

### ~~BUG-012: Obsidian search ranking did not distinguish exact title matches~~ — RESOLVED 2026-05-27

Extension: NpuObsidianExtension  
Severity: Low — search ranking could return longer-title notes above exact matches when backlinks differed  
Discovered: 2026-05-27 (audit) / Fixed: 2026-05-27

`ObsidianSearchService.Score()` gave +10 for any title substring match, including exact
matches. A note with title `Cathedral Notes` and 3 backlinks (total +13) could outrank
a note with an exact title match `cat` (+10 + whole-word +2 = +12).

Fix: exact title match now scores +15; substring match scores +10. This ensures exact title
matches always beat shorter-substring matches regardless of backlink count up to the cap
of +3. Two new tests added.

### ~~BUG-011: Obsidian backlink indexing missed Markdown links and path-qualified wiki targets~~ — RESOLVED 2026-05-27

Extension: NpuObsidianExtension  
Severity: Medium — notes with `[text](Note.md)` or `[[Folder/Note]]` links never generated backlinks  
Discovered: 2026-05-27 (audit) / Fixed: 2026-05-27

`ObsidianMarkdownParser.ExtractWikiLinks` only extracted `[[WikiLink]]` syntax.
Markdown `[text](path.md)` links were never extracted and never contributed to backlinks.
`BuildBacklinks` looked up link targets only by title and filename stem, missing
`[[Folder/Sub Note]]` links whose target had no entry keyed by the full relative path.

Fix:
- Added `ObsidianMarkdownParser.ExtractMarkdownLinks()` with a `[text](target)` regex that
  strips anchors, `.md` extension, `./` prefix, and ignores HTTP/HTTPS/obsidian:// URLs.
- `ObsidianIndexStore.IndexFile()` merges markdown links into the `WikiLinks` field (no schema change; existing index files gain the field from the next re-index).
- `BuildBacklinks` now adds relative-path-without-extension keys (forward slashes) to the
  lookup so `[[Folder/Note]]` resolves to `RelativePath = "Folder\Note.md"`.
- `NormalizeLinkTarget` helper strips anchors and `.md` from all link targets before lookup.
- Six new parser tests and the Obsidian extension and test suite both build clean.

## Resolved

### ~~BUG-010: Awake keeper not running after extension reload~~ — RESOLVED 2026-05-18

Extension: NpuAwakeExtension / NpuAwakeKeeper  
Severity: High — awake appeared active in UI but did not prevent sleep  
Discovered: 2026-05-18 / Fixed: 2026-05-18

Root issue:

When PowerToys kills and restarts the extension process (e.g. "Reload Command Palette extensions"), the keeper daemon is killed too. `state.json` kept the active override, so the UI showed "active" but nothing was holding a sleep-prevention request. `NpuAwakeCommandsProvider` had no startup logic to restart the daemon.

Fix:
- `NpuAwakeCommandsProvider`: calls `EnsureDaemonRunning()` at init when an unexpired override or schedules are present.
- `NpuAwakeKeeper/Program.cs`: continues to use `SetThreadExecutionState`, matching the PowerToys Awake approach. A short-lived attempt to switch to `PowerCreateRequest`/`PowerSetRequest` was reverted.

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
