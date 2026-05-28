# FEEDBACK FOR CLAUDE - READ THIS FIRST

Claude: this file exists in the repository root because Codex is auditing your work while you continue. `CLAUDE.md` and `AGENTS.md` both point here; read this file before making or committing further changes.

Audit snapshot: 2026-05-28 latest Codex pass. Current branch is `fix/clipboard-sync-audit`; current HEAD is `c2d5f1f`: `Merge feat/clipboard-cross-device-sync: sync text entries via shared folder`.

No material change since the prior audit pass: the same Clipboard sync/docs/test/icon findings below are still open.

## Verification

- `rtk dotnet build src\NpuClipboardExtension\NpuClipboardExtension.csproj -p:Platform=x64` passes: 3 projects, 0 errors, 0 warnings.
- `rtk dotnet test src\NpuTools.Tests\NpuTools.Tests.csproj -p:Platform=x64` passes: 120 tests.

## Current Worktree

Dirty files:

- `AGENTS.md` and `CLAUDE.md` contain Codex's audit-file pointers.
- `src/NpuClipboardExtension/ClipboardVisuals.cs` is currently dirty.
- `FEEDBACK_FOR_CLAUDE.md` is this audit file.

## Findings

### P1: Clipboard sync still does blocking shared-folder IO from `GetItems()`

`src/NpuClipboardExtension/Pages/ClipboardHistoryPage.cs:49-53` calls `_store.SyncFrom(syncFolder)` inside `GetItems()`.

That path reaches `src/NpuClipboardExtension/Shared/ClipboardSyncService.cs:42-49`, which checks the sync directory and enumerates every `*.json` file. For a OneDrive/Dropbox/network-backed folder, this can block the Command Palette COM/UI thread every time the history page is refreshed. This repeats the repo's known class of issues around blocking work in `GetItems()`.

Suggested fix: move sync import onto a background task or explicit refresh command, cache the last import result/time, and have `GetItems()` only render already-local state plus a loading/status row.

### P2: Roadmap still contradicts the shipped Clipboard sync feature

`docs/PROGRESS.md:17` and `docs/PROGRESS.md:136` say cross-device sync is shipped.

`docs/ROADMAP.md:26` still says: "Cross-device sync is intentionally not active yet" and describes `syncFolder` as a future placeholder.

Update `docs/ROADMAP.md` so the canonical roadmap matches the committed feature.

### P2: Obsidian completion status still contradicts remaining work

`docs/PROGRESS.md:19` marks `NpuObsidianExtension` as 100% with "All planned features shipped", and `docs/PROGRESS.md:141` marks the section as 100%.

The same file still lists `docs/PROGRESS.md:222` as next work: `Obsidian: bulk operations — multi-select delete/move`, and `docs/ROADMAP.md:45` says Obsidian remaining work is `bulk multi-select operations`.

Pick one truth before continuing: either bulk operations are still pending, in which case Obsidian should not be marked all-planned/100%, or bulk operations are out of scope/future and should be moved out of "What To Build Next".

### P2: Clipboard sync still has no focused tests

The shared test project only includes clipboard classifier coverage. There are no tests for `ClipboardSyncService`, `ClipboardStore.SyncFrom`, `SetSyncFolderPage`, or `SourceDevice` behavior.

Add deterministic tests for:

- writing only text entries to a temp sync folder,
- ignoring image/file entries,
- reading entries from another `SourceDevice`,
- skipping same-machine entries,
- avoiding duplicate IDs/content hashes when importing,
- pruning old sync files.

### P3: Current `ClipboardVisuals.cs` dirty diff converts icon escapes into pasted glyphs

The dirty edit in `src/NpuClipboardExtension/ClipboardVisuals.cs:8-26` changes the icon block from readable `\uXXXX` escapes into pasted glyph literals such as `new("")`, `new("")`, and `new("")`.

Repo guidance says icon glyphs should use `\uXXXX` escapes, not pasted glyphs. Please revert the existing icon literals back to escaped form and add the Sync icon as an escaped code point too. The current `SyncTag` uses ASCII (`synced - ...`), which is fine.

## Build Reminder

Before building Clipboard, stop the extension process if it is running:

```powershell
Stop-Process -Name "NpuClipboardExtension" -Force -ErrorAction SilentlyContinue
```

Otherwise the build can fail with `MSB3021`/`MSB3027` even when the code compiles.
