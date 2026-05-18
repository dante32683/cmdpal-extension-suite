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

## Tests

- `NpuTools.Tests`: xunit project targeting net9.0-windows10.0.26100.0. 37 tests covering:
  - `SlugServiceTests`: Slugify algorithm, BuildTargetFilename, ResolveCollision, IsAlreadyDateNamed, NormalizeExtension against Raycast parity fixtures.
  - `TextRewriteServiceTests`: all six rewrite-mode prompts validated for instruction text and format.
  - All tests pass with `dotnet test src/NpuTools.Tests/NpuTools.Tests.csproj`.

## Shell Projects

- NPU Notes
- NPU Dev Toolbox

## Next Work

- Add CI for restore/build.
- Publish release artifacts per extension.
- Implement NPU Notes (add/browse/delete/find related/search per migration plan Phase 5).
- Implement NPU Dev Toolbox (open workspace in Explorer/terminal/IDE per migration plan Phase 6).
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
