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
| NpuTextToolsExtension | Shipped (partial) | ~55% |
| NpuClipboardExtension | Shipped | ~92% |
| NpuNotesExtension | MVP shipped | ~55% |
| NpuObsidianExtension | M2 shipped | ~60% |
| NpuDevToolboxExtension | Shell only | 0% |

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

## NPU Text Tools — ~55%

Six rewrite modes shipped. Quick selected-text rewrite — the core P1 differentiator — is not done.

| Feature | Status |
|---|---|
| Six rewrite modes (grammar, formal, concise, bullets, simplify, custom) | ✅ |
| Result page with copy | ✅ |
| Custom two-step flow (instruction → text) | ✅ |
| Quick selected-text paste (TextSelectionHelper) | ❌ P1 |
| Quick selected-text review before paste | ❌ P1 |
| Selection diagnostics page | ❌ P1 |
| Quick mode / quick instruction settings | ❌ P1 |

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
| Time-window grouping display in list | not verified |

---

## NPU Notes — ~55%

File-backed Markdown MVP implemented. AI and semantic features remain future work.

Implementation guide: `docs/NPU_NOTES_EXTENSION_GUIDE.md`.

Raycast-inspired plan, adapted to Command Palette:
1. Notes Hub — ✅ default top-level entry showing recent and pinned notes, replacing Raycast's separate toggleable Notes window.
2. Create Note — ✅ zero-friction capture command. First line becomes title by default.
3. Search Notes — ✅ dynamic title/content search, with pinned notes first and recent notes as the empty-query view.
4. Browse Notes — ✅ category-filtered stack of Markdown files under `%UserProfile%\Documents\NpuNotes`.
5. Pin Notes — ✅ pinned notes sort first via `.notes-index.json` sidecar metadata.
6. Note Detail — ✅ Markdown preview with actions: open in editor, open folder, copy content, pin/unpin, delete.
7. Delete Note — ✅ move to Recycle Bin with confirmation.
8. Settings — ✅ notes root, default category, open-after-create, recent count, result count.
9. AI cleanup on create — future.
10. Find Related Notes — future Phi relatedness over recent/capped candidates.
11. Semantic fallback search — future keyword first, Phi relevance only when keyword results are sparse.

V1 should avoid building a full editor inside Command Palette. Store notes as normal Markdown files with YAML frontmatter, open them in the user's configured editor for rich editing, and keep Command Palette optimized for create/search/browse/action workflows.

Future: AppContentIndexer semantic index, rebuild action, RAG Q&A over notes, inline editor helper if Command Palette alone is too limited.

---

## NPU Dev Toolbox — 0%

Shell project. No features implemented.

Planned:
1. Open Workspace — detect from open Explorer windows, recent workspaces, explicit path; list candidates
2. Open in Explorer / Terminal / IDE actions
3. Windows Terminal support (`wt.exe` new-tab with profile and working directory)
4. IDE support (Cursor, VS Code, Windsurf, JetBrains, custom path)
5. Settings: default target, terminal choice, IDE choice, workspace detection timeout

Future: Commit Message command with Phi generation and `git commit` confirmation.

---

## What To Build Next

Roughly in priority order:

1. **Text Tools: selected-text quick rewrite** — biggest P1 gap; needs `TextSelectionHelper` binary added as packaged companion (same keeper pattern as Awake/Organize)
2. **Notes extension** — full Phase 5 from `RAYCAST_MIGRATION_PLAN.md`
5. **Dev Toolbox extension** — full Phase 6
6. **CI: restore/build pipeline** — build validation per push
7. **Publish: release artifacts** — per-extension MSIX publish flow
