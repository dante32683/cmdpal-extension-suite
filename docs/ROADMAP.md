# Roadmap

## Current Migration

Status: in progress

1. Consolidate Action Center, Simple Analytics, and NPU tools into one monorepo. Status: done.
2. Preserve available git history from old repos. Status: done for Action Center via `imported/action-center`; Simple Analytics had no `.git` repo.
3. Keep every extension separately packageable. Status: done; each project keeps its own package manifest.
4. Apply shared conventions across all imported extensions. Status: done for initial pass.
5. Verify solution build. Status: done for `dotnet build NpuCommandPaletteExtensions.sln -p:Platform=x64`.

## Implemented Extensions

- Action Center: Quick Settings dock button with settings-controlled toggle reset.
- Time Date Dock: individually addable configurable time and date dock buttons that open Notification Center.
- Media Controls Dock: media playback controls with compact dock subtitle suppression and native glyph controls.
- Simple Analytics: individually addable battery, Wi-Fi/network, and CPU dock/status views.
- NPU Awake: toggle, timed duration, until local time, schedules, dashboard, Smart Awake, fallback commands, and daemon integration.

## Implemented Extensions (continued)

- NPU Organize: screenshot rename proposals using `ImageDescriptionGenerator` (BriefDescription vision model → stopword-filtered 5-token slug), dry-run mode, watcher dashboard stub, hub page. AI naming falls back to time-digit slug when model unavailable. OrganizeKeeper daemon built and integrated — WatcherDashboardPage shows live state. Requires `Microsoft.WindowsAppSDK.AI` 1.8.47 and `systemAIModels` capability. Screenshot search: `ScreenshotIndexService` persists OCR text + AI description per file to `%LocalAppData%\NpuOrganize\index.json`; `ScreenshotSearchPage` (DynamicListPage) searches by content or description in real time (in-memory, sub-millisecond per keystroke); `IndexAllPage` indexes all screenshots in bulk; rename commands automatically upsert to index after each rename. End-to-end verified 2026-05-17 — rename → index → search round trip confirmed working.
- NPU Image Editor: OCR via OcrEngine, background removal via ImageObjectExtractor, 2x super-resolution via ImageScaler, hub + per-operation input pages. Uses built-in SDK `CopyTextCommand` and `OpenFileCommand` from the Toolkit namespace.
- NPU Text Tools: six rewrite modes (Fix Grammar, Make Formal, Make Concise, Bullet Points, Simplify, Custom) via Phi LanguageModel, hub + per-mode input pages. Custom mode uses two-step flow (instruction page → text page) matching Raycast UX. TestAiCommand removed from top-level command list.
- NPU Clipboard: standalone clipboard history extension with `NpuClipboardKeeper` background recorder. Stores local history at `%LocalAppData%\NpuClipboard\history.json`, supports searchable/filterable text, images, files, links, emails, and colors; copy, paste, paste as plain text, rename, pin, delete, hard-confirm delete all, bulk delete by recent time window, count-based retention, disabled application names, image OCR via `OcrEngine`, Ask Clipboard local/Phi search, and cross-device sync via shared folder (OneDrive/Dropbox/etc.) — writes text entries as per-entry JSON files in `{syncFolder}/clipboard-sync/`, merges remote entries in the background on page open (rate-limited), and prunes files older than 30 days hourly.
- NPU Notes: Markdown note hub with file-backed create, search, browse-by-category, preview/details, pin/unpin, open in editor, copy, reveal, settings, and Recycle Bin delete flows. Stores notes under `%UserProfile%\Documents\NpuNotes` by default with YAML frontmatter and `.notes-index.json` sidecar metadata.

## Tests

- `NpuTools.Tests`: xunit project targeting net9.0-windows10.0.26100.0. 53 tests covering:
  - `SlugServiceTests`: Slugify algorithm, BuildTargetFilename, ResolveCollision, IsAlreadyDateNamed, NormalizeExtension against Raycast parity fixtures.
  - `TextRewriteServiceTests`: all six rewrite-mode prompts validated for instruction text and format.
  - All tests pass with `dotnet test src/NpuTools.Tests/NpuTools.Tests.csproj`.

## Shell Projects

- NPU Dev Toolbox

## Next Work

- Add CI for restore/build.
- Publish release artifacts per extension.
- NPU Notes: Find Related Notes, semantic fallback search, AI cleanup on create, rebuild index, rename, and move are shipped. Remaining: RAG Q&A (future).
- NPU Obsidian: M1-M4 fully shipped (vault browser, persistent index, AI summarize/find-related/smart-capture, delete/rename/move). Bulk multi-select operations are a future enhancement outside the current migration scope.
- NPU Dev Toolbox: MVP fully shipped. Remaining: Open Explorer window detection (future).
- Continue Raycast migration using `RAYCAST_MIGRATION_PLAN.md`.

## OrganizeKeeper Design Notes

OrganizeKeeper is a separate background daemon exe (not a Command Palette extension) that watches the Screenshots folder via `FileSystemWatcher` and automatically renames new files as they are created — without the user opening Command Palette.

### Why it must be a separate exe
Command Palette extensions only run while the palette is active. A file watcher must be always-on, so it needs its own process lifetime.

### What it must do
1. Watch `%UserProfile%\Pictures\Screenshots` (or configured folder).
2. Debounce `Created`/`Changed` bursts (screenshots often fire both events).
3. For each stable new file, call `ImageDescriptionGenerator` to get a brief description, slugify it (same rules as `AiNamingService`), and rename the file.
4. Skip files already matching the `YYYY-MM-DD_` prefix.
5. Write heartbeat + state to `%LocalAppData%\NpuOrganize\state.json` so the Watcher Dashboard can show live status without IPC.
6. Honour a `stop.flag` file (written by `StartStopKeeperCommand`) for clean shutdown.

### MSIX identity requirement
`ImageDescriptionGenerator` requires MSIX packaged identity and the `systemAIModels` restricted capability. The keeper exe must be registered via `Add-AppxPackage -Register -ExternalLocation` with a `Package.appxmanifest` declaring `systemAIModels` — same pattern as the Raycast `NpuOrganizeBridge.Identity`.

### Reference implementation
The Raycast keeper at `C:\Portable\Raycast\npu-ext-suite\npu-organize-ext\keeper\` is a near-complete reference:
- `Program.cs` — `watch`, `status`, `process-one`, `parity-check` modes
- `Watcher.cs` — `FileSystemWatcher` + debounce logic
- `StateStore.cs` — state.json + config.json + log file management
- `SlugGenerator.cs` — C# port of slug.ts (already ported into `AiNamingService.Slugify`)
- `BridgeClient.cs` — calls NpuBridge.exe; replace with direct `ImageDescriptionGenerator` call

### WatcherDashboardPage current state
`WatcherDashboardPage.cs` looks for `NpuOrganizeKeeper.exe` in `AppContext.BaseDirectory` (beside the extension exe). If absent, shows "OrganizeKeeper not installed". Once the exe is built and placed there, the dashboard will show running/stopped state and start/stop controls.
