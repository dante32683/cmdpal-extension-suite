# Extension Progress

Feature-level completion status across all planned extensions. Source of truth for "what's left."

## Summary

| Extension | Status | % Complete |
|---|---|---:|
| ActionCenterExtension | Shipped | 100% |
| TimeDateDockExtension | Shipped | 100% |
| MediaControlsExtension | Shipped | 100% |
| SimpleAnalyticsExtension | Shipped | 100% |
| NpuAwakeExtension | Shipped | 100% |
| NpuOrganizeExtension | Shipped | 100% |
| NpuImageEditorExtension | Shipped | ~98% |
| NpuTextToolsExtension | Shipped (partial) | ~80% |
| NpuClipboardExtension | Shipped | ~92% |
| NpuNotesExtension | AI features shipped | ~75% |
| NpuObsidianExtension | M3 shipped | ~80% |
| NpuDevToolboxExtension | MVP shipped | ~80% |

---

## Dock Extensions

All four dock extensions are complete. No planned work remaining.

| Feature | Status |
|---|---|
| ActionCenter quick-settings toggle | ✅ |
| TimeDateDock configurable time/date buttons | ✅ |
| MediaControls playback, volume, session switching | ✅ |
| SimpleAnalytics battery, Wi-Fi, CPU dock views | ✅ |

---

## NPU Awake — 100%

All priority features from the migration plan are shipped.

| Feature | Status |
|---|---|
| Toggle awake / let sleep | ✅ |
| Awake For (timed minutes) | ✅ |
| Awake Until (local time) | ✅ |
| Screen-off mode | ✅ |
| Awake Dashboard | ✅ |
| Schedules (add/pause/resume/delete) | ✅ |
| Smart Awake (Phi natural-language) | ✅ |
| Dock band (status + toggle) | ✅ |
| AwakeKeeper daemon | ✅ |

---

## NPU Organize — 100%

All P0 features shipped. Screenshot search with relevance ranking and context actions added beyond original scope.

| Feature | Status |
|---|---|
| Rename new screenshots (AI slug) | ✅ |
| Dry run mode | ✅ |
| Watcher dashboard | ✅ |
| OrganizeKeeper daemon | ✅ |
| Dock band (watcher state) | ✅ |
| Screenshot content search (OCR + AI index) | ✅ bonus |
| Search relevance ranking (desc +3, OCR +2, whole-word +1, recency tiebreak) | ✅ bonus |
| Search shows recent screenshots by default | ✅ bonus |
| Context actions: Copy Image (Ctrl+C), Copy Path (Ctrl+Shift+C), Open File Location (Ctrl+Shift+E) | ✅ bonus |
| Slug parity tests | ✅ |
| Downloads triage / monthly subfolders | deferred |
| Multi-folder watch | deferred |

---

## NPU Image Editor — ~98%

All planned features shipped. ImageForegroundExtractor for automatic background removal, 2×/4×/8× super resolution, image browser, and settings system.

| Feature | Status |
|---|---|
| OCR (copy/save text) | ✅ |
| Remove background (ImageForegroundExtractor, automatic) | ✅ |
| 2× super resolution | ✅ |
| 4× super resolution | ✅ |
| 8× super resolution | ✅ |
| Image browser (Pictures dir, recency sort, path fallback) | ✅ |
| Settings (scale factor, auto-open, OCR auto-copy, OCR text file) | ✅ |
| Context actions on file results: Open File Location (Ctrl+Shift+E), Copy Path (Ctrl+Shift+C) | ✅ bonus |
| Make Sticker (WebP subject extraction) | future |
| Clipboard image input | future |

---

## NPU Text Tools — ~80%

Six rewrite modes + Quick Rewrite with selection capture shipped.

| Feature | Status |
|---|---|
| Six rewrite modes (grammar, formal, concise, bullets, simplify, custom) | ✅ |
| Result page with copy | ✅ |
| Custom two-step flow (instruction → text) | ✅ |
| Quick Rewrite page (capture selected text + Phi rewrite) | ✅ |
| Selection capture via Ctrl+C + clipboard polling | ✅ |
| Toast notification on completion | ✅ |
| Quick Rewrite with typed text (skip capture) | ✅ |
| Quick selected-text review before paste | deferred |
| Selection diagnostics page | deferred |
| Quick mode default setting | deferred |

The selected-text quick rewrite is the largest remaining gap. It requires a `TextSelectionHelper` binary that hides Command Palette, captures clipboard, sends Ctrl+C, reads selection, runs Phi rewrite, and optionally pastes result.

---

## NPU Clipboard — ~92%

MVP scope is fully implemented. Cross-device sync is intentionally deferred.

| Feature | Status |
|---|---|
| Clipboard history (text, images, files, links, emails, colors) | ✅ |
| Search and type filter | ✅ |
| Copy / paste / paste as plain text | ✅ |
| Rename / pin / delete entry | ✅ |
| Delete all (multi-step confirm) | ✅ |
| Bulk delete by time window | ✅ |
| Count-based retention (200–unlimited) | ✅ |
| Disabled application names | ✅ |
| Image OCR | ✅ |
| Ask Clipboard (local + Phi semantic search) | ✅ |
| NpuClipboardKeeper background recorder | ✅ |
| Open File Location (Ctrl+Shift+E) for image + file entries | ✅ bonus |
| Recorder toggle shortcut Ctrl+R on Clipboard History context menu | ✅ |
| Added to Refresh-ExtensionRegistrations.ps1 deploy script | ✅ (was missing) |
| Cross-device sync via sync folder | deferred |
| Time-window grouping display in list | ✅ (code-verified 2026-05-27) |

---

## NPU Obsidian — ~80%

M3 shipped. Vault index, AI summarization, Find Related Notes, and Smart Capture all implemented.

| Feature | Status |
|---|---|
| Vault browser (Hub, Search, Browse, Preview) | ✅ M1 |
| Vault metadata (pin, record opened) | ✅ M1 |
| Quick Append, Copy URI/Link, Daily Note | ✅ M1 |
| Persistent vault index (vault-index.json) | ✅ M2 |
| IndexVaultPage (progress, stats) | ✅ M2 |
| Index-backed search (backlinks, AI summary score) | ✅ M2 |
| Unit tests (parser, search scoring) | ✅ M2 |
| Phi summarization (SummarizeNotePage + save to index) | ✅ M3 |
| Find Related Notes (deterministic scoring + Phi rerank) | ✅ M3 |
| Smart Capture (DynamicListPage → ProposalPage) | ✅ M3 |
| Smart Capture creates note with AI-proposed folder/tags | ✅ M3 |
| Summarize/Find Related in note preview and MoreCommands | ✅ M3 |
| Delete note | future M4 |
| Bulk vault operations | future M4 |

---

## NPU Notes — ~75%

File-backed Markdown MVP + AI features implemented.

Implementation guide: `docs/NPU_NOTES_EXTENSION_GUIDE.md`.

| Feature | Status |
|---|---|
| Notes Hub (recent, pinned) | ✅ |
| Create Note (zero-friction capture) | ✅ |
| Search Notes (keyword, pinned-first) | ✅ |
| Browse Notes (by category) | ✅ |
| Pin Notes (index sidecar) | ✅ |
| Note Detail (open, copy, pin, delete) | ✅ |
| Delete Note (Recycle Bin, confirmation) | ✅ |
| Settings (root, category, recent count) | ✅ |
| Find Related Notes (deterministic scoring + Phi rerank) | ✅ |
| Semantic fallback search (Phi when keyword results < 3) | ✅ |
| AI cleanup on create | future |

V1 should avoid building a full editor inside Command Palette. Store notes as normal Markdown files with YAML frontmatter, open them in the user's configured editor for rich editing, and keep Command Palette optimized for create/search/browse/action workflows.

Future: AppContentIndexer semantic index, rebuild action, RAG Q&A over notes, inline editor helper if Command Palette alone is too limited.

---

## NPU Dev Toolbox — ~80%

MVP shipped. Workspace detection, Explorer/Terminal/IDE launch, recent workspaces, settings.

| Feature | Status |
|---|---|
| Dev Toolbox Hub (recent + scanned workspaces) | ✅ |
| Workspace detection (~/repos, ~/source/repos, etc. with .git/.sln/package.json markers) | ✅ |
| Open in Explorer | ✅ |
| Open in Terminal (Windows Terminal / PowerShell / Cmd / Custom) | ✅ |
| Open in IDE (VS Code / Cursor / Windsurf / Custom) | ✅ |
| Quick Open commands (Explorer / Terminal / IDE shortcuts from top level) | ✅ |
| Recent workspaces (persistent JSON, add-on-open, remove action) | ✅ |
| Settings (terminal choice, IDE choice, custom exe paths) | ✅ |
| Manual path entry via search box | ✅ |
| Open Explorer window detection | future |
| Commit Message with Phi generation | future |
| Windows Terminal profile selection | future |

---

## What To Build Next

Roughly in priority order:

1. **Clipboard: time-window grouping display** — currently listed as "not verified"; verify or implement
2. **Notes: AI cleanup on create** — run Phi grammar/title fix after quick-capture
3. **Obsidian M4: delete note, bulk vault operations** — see feature table above
4. **Dev Toolbox: Commit Message with Phi** — generate commit message from `git diff` for current workspace
5. **CI: restore/build pipeline** — build validation per push
6. **Publish: release artifacts** — per-extension MSIX publish flow
