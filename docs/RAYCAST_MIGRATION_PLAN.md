# Raycast Migration Plan

This document is the decision-complete plan for migrating the Raycast extension suite at
`C:\Portable\Raycast\npu-ext-suite` to PowerToys Command Palette extensions.

This is planning material. It is not current architecture truth until implementation starts and the
canonical docs are updated to match shipped code.

## Summary

Build the migration as a suite of separately installable Command Palette extensions in one solution,
not as one monolithic extension. Each feature area gets its own MSIX/package identity, provider ID,
commands, settings, and optional helpers. A shared class library is allowed as a build-time dependency,
but each shipped extension package must include what it needs and run standalone.

Priority order:

1. Awake: complete current feature set, including dashboard, schedules, Smart Awake, and dock band.
2. Organize: complete current screenshot rename and watcher feature set, including dock band.
3. Image Editor priority features: remove background, super resolution, OCR.
4. Text Tools priority features: six rewrite modes plus best-effort selected-text quick rewrite.
5. Notes priority features: add notes, category folders, browse notes, delete note, find related, semantic fallback.
6. Dev Toolbox priority feature: open active/recent workspace in Explorer, terminal, and IDE.
7. Move lower-priority implemented Raycast features and future Raycast polish into later phases.

Use in-process C# services for AI and WinRT bridge logic rather than spawning the Raycast `NpuBridge.exe`
files. Keep only true helper/daemon binaries as packaged companions: `AwakeKeeper`, `OrganizeKeeper`,
and a Command Palette-adapted `TextSelectionHelper`.

## Architecture And Public Interfaces

- Create separate Command Palette extension projects:
  - `NpuAwakeExtension`
  - `NpuOrganizeExtension`
  - `NpuImageEditorExtension`
  - `NpuTextToolsExtension`
  - `NpuNotesExtension`
  - `NpuDevToolboxExtension`
- Add a shared build-time library, `NpuTools.Shared`, packaged into each extension:
  - AI readiness helpers: `GetReadyState`, optional `EnsureReadyAsync`, clear disabled/unsupported errors.
  - Phi/LAF unlock helper copied from the Raycast bridges, using UTF-8 token hashing.
  - JSON result helpers for consistent success/error handling.
  - File/path utilities, process launcher helpers, settings serialization helpers.
- Each extension must define:
  - Unique MSIX `Identity Name`
  - Unique COM class GUID
  - Unique provider `Id`, for example `com.local.nputools.awake`
  - `runFullTrust` capability
  - `systemAIModels` only when the extension uses Windows AI APIs
- Use Command Palette pages:
  - `ListPage` for browsable item lists and action hubs.
  - `ContentPage` plus `MarkdownContent` for status/result dashboards.
  - `FormContent` for user input forms.
  - `Settings` page for preferences.
- Use dock bands only where persistent state matters:
  - Awake status/toggle band.
  - Organize watcher status/toggle band.
  - Optional later: image/text shortcut bands.

## Implementation Plan By Extension

### Phase 0: Foundation

- Replace the single scaffold-style app with a multi-project solution.
- Set provider IDs and implement `GetDockBands()` where needed.
- Add packaging/build docs for separate install/publish flows.
- Add shared settings storage:
  - Per-extension JSON under `%LocalAppData%\NpuTools\<ExtensionName>\`.
  - Command Palette settings page mirrors those values.
- Add shared command result behavior:
  - One-off actions return `Dismiss()`.
  - Dashboards and status pages return `KeepOpen()`.
  - Destructive actions use `CommandResult.Confirm()`.

### Phase 1: NPU Awake

Implement the entire currently implemented Raycast plugin.

- Awake toggle:
  - Starts or stops an indefinite keep-awake override.
  - Supports default mode setting: normal display-awake or screen-off mode.
- Awake For:
  - Form accepts minutes.
  - Writes a timed override and starts/updates `AwakeKeeper`.
- Awake Until:
  - Form accepts local time such as `17:30`.
  - Extension computes the UTC expiry timestamp; the model never does time math.
- Let Sleep:
  - Cancels active override and allows sleep.
- Awake Dashboard:
  - Shows current mode, expiry, daemon state, active schedule state, and warnings.
  - Actions: start indefinite, start timed, start until, screen-off, let sleep, open schedules, stop daemon.
- Awake Schedules:
  - List existing schedules from `schedules.json`.
  - Add/pause/resume/delete schedules.
  - Atomic writes: temp file then rename.
- Smart Awake:
  - Form/text command sends natural language to Phi intent extractor.
  - Intent extractor returns strict JSON only: action, mode, duration fields, schedule fields.
  - TypeScript/Raycast orchestration becomes C# orchestration in the extension.
  - All timestamp math remains deterministic in C#.
- Dock band:
  - One band showing status: `Awake`, `Sleeping Allowed`, or `Scheduled`.
  - Primary click toggles awake/let sleep using the documented dock toggle pattern.

Keep `AwakeKeeper.exe` as a companion binary because schedules and sleep prevention must survive
Command Palette process reloads.

### Phase 2: NPU Organize

Implement the entire currently implemented Raycast plugin, excluding future brainstorm work.

- Rename New Screenshots:
  - Scans configured screenshots folder.
  - Skips already date-named files.
  - Uses in-process Image Description plus OCR service.
  - Generates deterministic slug and collision-safe target path.
  - Shows proposals in a list.
  - Actions: rename selected, rename all, open folder.
- Dry Run Screenshot Rename:
  - Same scan/proposal path.
  - No write action.
- Screenshot Watcher:
  - Dashboard shows running/stopped, PID, heartbeat, counters, last activity, recent log tail.
  - Actions: start watcher, stop watcher, restart watcher, open support folder.
- OrganizeKeeper:
  - Keep as packaged companion binary.
  - Uses `FileSystemWatcher`, debounce, battery skip, ignore regex, state cursor, stop flag.
  - Calls the in-package screenshot-title service through a supported IPC boundary or a small local `NpuOrganizeWorker.exe`.
- Slug parity:
  - Port existing slug rules to shared C#.
  - Keep a parity test matching Raycast fixtures.
- Dock band:
  - Shows watcher state and recent processed/error count.
  - Click opens Screenshot Watcher dashboard or toggles start/stop.

Do not implement Downloads triage, monthly subfolders, multi-folder watch, or telemetry polish in v1.

### Phase 3: NPU Image Editor

Priority features first.

- Image Tools Hub:
  - List selected Explorer images plus actions.
  - Also allow manual file picker/folder input if Explorer selection is unavailable.
- Explorer selection service:
  - Enumerate open Explorer windows and selected items.
  - Do not rely solely on foreground window because Command Palette steals focus.
- Remove Background:
  - Port `ImageObjectExtractor` bridge logic into an in-process service.
  - Output next to source as processed PNG.
- Super Resolution:
  - Port `ImageScaler`.
  - Support 2x and 4x only.
  - Do not expose 8x unless verified on target hardware.
- OCR:
  - Port `Windows.Media.Ocr.OcrEngine`.
  - Convert source bitmap to `Bgra8`.
  - Result page supports copy all and save `.txt`.
- Settings:
  - Default scale factor.
  - Auto-open result.
  - Show success messages.
  - Ensure model ready.
  - OCR auto-open text file.

Future polish:

- Make Sticker:
  - Port subject extraction first.
  - Replace Raycast/Jimp/WebP pipeline with a .NET image/WebP implementation.
- Modify Image full hub:
  - Consolidate all image actions under one hub.
- Clipboard image input:
  - Add after Explorer and manual file selection are stable.

### Phase 4: NPU Text Tools

Priority features plus best-effort selected-text quick rewrite.

- Rewrite Text Hub:
  - One hub lists modes: Fix Grammar, Make Formal, Make Concise, Bullet Points, Simplify, Custom Rewrite.
  - Each mode opens a form.
- Six form commands:
  - Keep each mode available as a top-level command for searchability.
  - Fix Grammar.
  - Make Formal.
  - Make Concise.
  - Bullet Points.
  - Simplify.
  - Custom Rewrite.
- Phi rewrite service:
  - Port `LanguageModel` logic from the Raycast text bridge.
  - Modes map exactly to current prompts.
  - Custom mode accepts `{ instruction, text }`.
  - Use optional `ensureModelReady`.
- Result page:
  - Shows rewritten text.
  - Actions: copy, paste over captured selection when available, try another mode.
- Selected Text Quick Rewrite:
  - Provide two top-level commands:
    - `Paste Selection (Quick)`
    - `Review Selection (Quick)`
  - Settings define quick mode and quick custom instruction.
  - Implementation is best-effort:
    - Hide/dismiss Command Palette.
    - Helper waits until foreground is not PowerToys/Command Palette.
    - Snapshot text clipboard.
    - Send Ctrl+C.
    - Read selected text from clipboard.
    - Run Phi rewrite.
    - For paste mode: copy result, send Ctrl+V, then restore previous text clipboard.
    - For review mode: open a result page before paste.
  - Adapt `TextSelectionHelper` from Raycast to detect PowerToys/Command Palette instead of Raycast.
  - Show diagnostics when focus never returns or no text is captured.
- Diagnostics:
  - Helper path.
  - Noop verification.
  - Copy/paste dry run.
  - Last failure reason.

Limitations to document: elevated apps, RDP, apps that block clipboard, apps with nonstandard selections,
and rich clipboard loss.

Future polish:

- Replace applicable modes with `TextRewriter` where the API fits.
- Add a resident global hotkey helper only if Command Palette invocation is not fast enough.

### Phase 5: NPU Notes

Priority features first. Raycast Notes is centered on three fast commands: open/toggle notes, create note, and search notes. Command Palette should adapt that into a notes hub plus direct create/search commands, with Markdown files as the storage and editing surface.

- Notes Hub:
  - Top-level command that shows pinned notes first, then recent notes.
  - Replaces Raycast's toggleable notes window; Command Palette is the navigator/action surface.
  - Actions: create note, search notes, browse categories, open notes folder.
- Add Note:
  - Zero-friction capture command.
  - First line becomes the title by default, matching Raycast Notes behavior.
  - Form accepts rough note text.
  - Optional clipboard prefill.
  - Optional Phi cleanup formats rough text into title/category/content.
  - Save Markdown with YAML frontmatter.
  - Category folders are created automatically.
  - Default notes folder: `%UserProfile%\Documents\NpuNotes`.
- Category folders:
  - Preserve Raycast categories: work, school, personal, tasks, ideas, health, finance, people, projects, misc.
- Browse Notes:
  - List notes grouped/filterable by category.
  - Open note detail as markdown.
  - Actions: open in editor, open folder, copy content, delete.
- Pin Notes:
  - Pinned notes sort to the top of hub, browse, and search views.
  - Store pin order in frontmatter or a small sidecar index.
- Delete Note:
  - Confirmation required.
  - Move to Recycle Bin, not permanent delete.
- Find Related Notes:
  - Select current note.
  - Run Phi relatedness over recent/capped candidates.
  - Show ranked related notes.
- Search Notes:
  - Keyword search first.
  - Semantic fallback using Phi relevance when keyword results are scarce.
  - Keep caps/settings for max semantic checks/results.
- Index hooks:
  - Keep AppContentIndexer out of the first notes milestone unless the basic flows are stable.
  - Preserve the design so AppContentIndexer can replace or supplement semantic fallback later.

Future polish:

- AppContentIndexer semantic index.
- Rebuild index action.
- Post-save related links and `related:` frontmatter.
- RAG question-answering over notes.
- Inline editor/helper only if opening Markdown in the user's editor is not enough.

### Phase 6: NPU Dev Toolbox

Implement only the requested workspace opener as v1.

- Open Workspace:
  - Detect active workspace from:
    - Explicit path argument/input.
    - Open Explorer windows.
    - Recent workspaces.
    - Git repo prioritization.
  - Display candidates in a list.
  - Actions:
    - Open in Explorer.
    - Open in Terminal.
    - Open in IDE.
    - Open all selected defaults.
  - Store recent workspaces locally.
- Terminal support:
  - Windows Terminal `wt.exe`, PowerShell 7, Windows PowerShell, cmd, custom path.
  - Preserve known `wt.exe` argument rules: `new-tab -p <profile> -d <folder>`.
- IDE support:
  - Cursor, VS Code, Windsurf, JetBrains/custom exe/lnk where possible.
- Settings:
  - Default open target.
  - Terminal choice/profile/custom path.
  - IDE choice/custom path.
  - Workspace detection timeout.
  - Success messages.

Future polish:

- Commit Message command.
- `cwd-of-pid` terminal cwd detection if needed.
- Phi commit generation and `git commit` execution with confirmation.

## Feature Priority Matrix

| Extension | Feature | Priority | Effort |
|---|---|---:|---:|
| Awake | Manual awake, timed, until, screen-off, let sleep | P0 | M |
| Awake | Dashboard + dock band | P0 | M |
| Awake | Schedules + daemon | P0 | L |
| Awake | Smart Awake | P0 | L |
| Organize | Manual screenshot rename | P0 | M |
| Organize | Dry run | P0 | S |
| Organize | Watcher hub + keeper | P0 | L |
| Organize | Dock band | P0 | M |
| Image | Remove background | P1 | M |
| Image | Super resolution | P1 | S-M |
| Image | OCR | P1 | S-M |
| Image | Make sticker | Future | L |
| Image | Clipboard image input | Future | M-L |
| Text | Six form rewrite modes | P1 | M |
| Text | Quick selected-text paste/review | P1 | L-XL |
| Text | Diagnostics | P1 | S-M |
| Notes | Add/category/browse/delete | P2 | L |
| Notes | Find related + semantic fallback | P2 | L |
| Notes | AppContentIndexer | Future | L |
| Dev Toolbox | Open workspace in Explorer/terminal/IDE | P2 | M-L |
| Dev Toolbox | Commit Message | Future | L |
| Organize | Downloads triage / monthly folders / multi-folder | Future | XL |

## Test Plan

- Build/deploy:
  - Each extension builds independently.
  - Each MSIX registers independently.
  - Command Palette reload shows only installed extensions.
  - Provider IDs are non-empty and dock bands persist.
- AI readiness:
  - Ready, NotReady with ensure, DisabledByUser, and unsupported hardware paths show clear errors.
  - First-run model preparation does not crash the COM server.
- Awake:
  - Indefinite, timed, until, screen-off, let sleep.
  - Schedule active/inactive windows.
  - Daemon survives palette reload.
  - Dock band reflects state and toggles correctly.
- Organize:
  - Dry run never renames.
  - Rename handles collision, already-named skip, max size, ignored pattern.
  - Watcher debounce handles Snipping Tool/Game Bar-style delayed writes.
  - Battery skip works.
  - Slug parity tests pass.
- Image:
  - Explorer selection and manual file input.
  - Remove background, 2x/4x upscale, OCR on PNG/JPG/WebP where supported.
  - OCR handles non-Bgra8 conversion.
  - Failure from unsupported image/model appears as one clean error.
- Text:
  - Each rewrite mode returns result only, no explanation.
  - Custom instruction binds correctly.
  - Quick selected-text works in Notepad, Edge text field, VS Code/Cursor.
  - Failure diagnostics for no selection and focus timeout.
  - Clipboard restoration verified for plain text.
- Notes:
  - Add note creates category folder and valid Markdown/frontmatter.
  - Browse/search parse existing notes.
  - Delete moves to Recycle Bin.
  - Related/semantic fallback respects caps.
- Dev Toolbox:
  - Workspace detection with focused Explorer, non-focused Explorer, explicit path, and recent workspace.
  - Terminal/IDE launchers do not leave stray consoles.
  - Windows Terminal profile and working directory are correct.

## Assumptions And Defaults

- Use separate installable extensions, one per Raycast plugin area.
- Use in-process C# ports of bridge logic, not Raycast `assets/bin/NpuBridge.exe`.
- Keep helper/daemon exes only where they are functionally required.
- Implement selected-text quick rewrite as best effort, not perfect Raycast parity.
- Prioritize the user-requested features before lower-priority implemented Raycast features.
- Do not implement Raycast future brainstorm items in v1 unless listed as current implemented behavior.
- Default notes folder changes from `RaycastNotes` to `NpuNotes` for Command Palette, unless preserving existing Raycast notes is explicitly required later.
- Command Palette settings replace Raycast preferences; where a Raycast command preference existed, use the nearest extension settings page option.
