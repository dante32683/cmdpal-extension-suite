# NPU Notes Extension Guide

This guide is the implementation plan for turning `src/NpuNotesExtension` from a Phase 0 shell into a Raycast-inspired Command Palette notes extension.

## Product Shape

Raycast Notes has three primary commands:

- Raycast Notes: toggles the notes window.
- Create Note: creates a note and opens the editor immediately.
- Search Notes: searches titles and note content.

Command Palette should adapt that model instead of cloning the Raycast window. The extension should be a fast command surface for capture, search, browse, preview, and actions. Rich editing should happen in the user's normal Markdown editor.

## V1 Scope

Status: implemented for the file-backed MVP. AI items remain deferred.

Build these in order:

1. Notes Hub — implemented
2. Create Note — implemented
3. Search Notes — implemented
4. Browse Notes — implemented
5. Note Detail — implemented
6. Pin/Unpin Note — implemented
7. Delete Note — implemented
8. Settings — implemented

Defer these until the core file-backed flows are stable:

- Phi cleanup/category inference for created notes.
- Find Related Notes.
- Semantic fallback search.
- AppContentIndexer integration.
- Inline rich editor/helper.

## UX Contract

Top-level commands:

| Command | Type | Behavior |
|---|---|---|
| `Notes` | `ListPage` | Hub with pinned notes, recent notes, and utility rows. Replaces Raycast's toggle command. |
| `Create Note` | `DynamicListPage` | Type/paste note text, create a Markdown file, then optionally open it. |
| `Search Notes` | `DynamicListPage` | Search title, body, category, and tags. Empty query shows pinned + recent. |
| `Browse Notes` | `ListPage` or `DynamicListPage` | Category entry points plus all notes grouped by category. |

Context actions on each note row:

- Open Note
- Open In Editor
- Open File Location
- Copy Content
- Copy Path
- Pin/Unpin
- Delete

Use `RequestedShortcut` on primary context actions. Recommended chords:

| Action | Shortcut |
|---|---|
| Copy Content | `Ctrl+C` |
| Copy Path | `Ctrl+Shift+C` |
| Open File Location | `Ctrl+Shift+E` |
| Pin/Unpin | `Ctrl+P` |
| Delete | `Ctrl+Shift+Delete` |

Do not duplicate the primary action in `MoreCommands`; the SDK inserts it automatically.

## Storage

Default root:

```text
%UserProfile%\Documents\NpuNotes
```

Categories:

```text
work
school
personal
tasks
ideas
health
finance
people
projects
misc
```

File layout:

```text
%UserProfile%\Documents\NpuNotes\
  ideas\
    2026-05-24_1530_build-notes-extension.md
  work\
    2026-05-24_1605_manager-1-1.md
  .notes-index.json
```

The `.notes-index.json` sidecar should track mutable UI metadata that should not force frequent Markdown rewrites:

```json
{
  "pinned": [
    { "path": "ideas/2026-05-24_1530_build-notes-extension.md", "pinOrder": 0 }
  ],
  "recent": [
    { "path": "work/2026-05-24_1605_manager-1-1.md", "openedUtc": "2026-05-24T23:05:00Z" }
  ]
}
```

Each note file should remain portable Markdown with simple YAML frontmatter:

```markdown
---
id: 0c681693-4b3a-4d10-9898-0be8187927d0
title: Build notes extension
category: ideas
createdUtc: 2026-05-24T22:30:00Z
updatedUtc: 2026-05-24T22:30:00Z
tags: notes, cmdpal
---

# Build notes extension

First line becomes the title by default.
```

Implementation note: keep the frontmatter parser intentionally strict. Parse only the keys this extension writes, tolerate missing frontmatter for imported Markdown files, and fall back to the first heading or first non-empty line for title.

## File Map

Replace the current Phase 0 provider with extension-specific files:

```text
src/NpuNotesExtension/
  NpuNotesCommandsProvider.cs
  KeyChords.cs
  Models/
    NoteEntry.cs
    NotesIndex.cs
    NotesSettings.cs
  Services/
    NotesStore.cs
    NotesIndexStore.cs
    NotesSearchService.cs
    NotesSettingsManager.cs
    NotesAiService.cs          # milestone 2
  Pages/
    NotesHubPage.cs
    CreateNotePage.cs
    SearchNotesPage.cs
    BrowseNotesPage.cs
    NoteDetailPage.cs
    DeleteNotePage.cs
    FindRelatedPage.cs         # milestone 2
  Commands/
    OpenNoteCommand.cs
    OpenNoteLocationCommand.cs
    CopyNoteContentCommand.cs
    CopyNotePathCommand.cs
    TogglePinNoteCommand.cs
    DeleteNoteCommand.cs
    CreateBlankNoteCommand.cs
    CreateNoteFromClipboardCommand.cs
```

Keep services free of Command Palette SDK types. Pages and commands should adapt service models into `ListItem`, `Details`, and `CommandContextItem`.

## Models

`NoteEntry` should include:

- `Id`
- `Title`
- `Category`
- `FilePath`
- `RelativePath`
- `CreatedUtc`
- `UpdatedUtc`
- `Tags`
- `Body`
- `IsPinned`
- `PinOrder`
- `Snippet`

`NotesSettings` should include:

- `NotesRoot`
- `DefaultCategory`
- `OpenAfterCreate`
- `MaxRecentNotes`
- `MaxSearchResults`
- `UseAiCleanupOnCreate` (off by default until AI milestone)
- `MaxSemanticCandidates` (AI milestone)

## Services

### NotesStore

Responsibilities:

- Ensure the notes root and category folders exist.
- Load all `.md` files under the root.
- Parse frontmatter and body.
- Create notes with atomic writes.
- Rename/move notes if category or title changes later.
- Move deleted notes to the Recycle Bin.
- Return plain service models, not SDK items.

Core methods:

```csharp
IReadOnlyList<NoteEntry> GetAll();
IReadOnlyList<NoteEntry> GetRecent(int maxCount);
IReadOnlyList<NoteEntry> GetPinned();
NoteEntry? GetByPath(string path);
NoteEntry Create(string rawText, string? category = null, bool openAfterCreate = true);
NoteEntry CreateBlank(string? category = null, bool openAfterCreate = true);
void DeleteToRecycleBin(string path);
```

For Recycle Bin delete, prefer `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(..., RecycleOption.SendToRecycleBin)` unless that causes packaging issues. If it does, use the same Explorer/Shell pattern used elsewhere in the repo.

### NotesIndexStore

Responsibilities:

- Load/save `.notes-index.json`.
- Track pin order.
- Track recently opened notes.
- Prune entries whose files no longer exist.

### NotesSearchService

Keyword ranking:

- Title match: +5
- Tag/category match: +3
- Body match: +2
- Whole-word match: +1
- Pinned boost: +2
- Recent tie-breaker

Search should be synchronous and in-memory. Load files once per `GetItems()` refresh, then filter on each `UpdateSearchText()` call. Do not perform disk I/O for every keystroke.

### NotesAiService

Add only after the V1 file-backed flows work.

Responsibilities:

- Infer title/category/body from rough text.
- Score related notes.
- Provide semantic fallback search when keyword results are sparse.

Any Phi call must use the lazy async result-page pattern. Do not call `.GetAwaiter().GetResult()` from `GetItems()`, `UpdateSearchText()`, or `Invoke()`.

When this service is added:

- Add `Microsoft.WindowsAppSDK.AI` if needed.
- Add `rescap:Capability Name="systemAIModels"` to `Package.appxmanifest`.
- Keep AI settings disabled by default if model readiness is uncertain.

## Pages

### NotesHubPage

Extends `ListPage`.

Rows:

1. Create Note
2. Search Notes
3. Browse Notes
4. Pinned notes
5. Recent notes
6. Open Notes Folder

Use note rows that navigate to `NoteDetailPage`.

### CreateNotePage

Extends `DynamicListPage`.

Behavior:

- Placeholder: `Type a note, or paste text here...`
- Empty query rows:
  - Create Blank Note
  - Create Note From Clipboard
  - Open Notes Folder
- Non-empty query:
  - Create Note row with preview subtitle.

Creation behavior:

- First non-empty line becomes the title.
- Remaining text becomes the body.
- If the first line is too long, slug and title should be truncated for display/file path, not for body content.
- Save to the default category initially.
- If `OpenAfterCreate` is true, open the file in the configured/default editor.

### SearchNotesPage

Extends `DynamicListPage`.

Behavior:

- Empty query shows pinned notes, then recent notes.
- Non-empty query ranks title/content/category/tag matches.
- Each result row opens `NoteDetailPage`.
- Each row has Details body with a Markdown preview and metadata rows.

### BrowseNotesPage

Start simple:

- Empty query shows categories as rows.
- Selecting a category opens a filtered `BrowseNotesPage`.
- Category page lists pinned notes in that category first, then updated-descending notes.

Later, make it a `DynamicListPage` that filters category and title text.

### NoteDetailPage

Extends `ListPage`.

Rows:

- Open In Editor
- Copy Content
- Copy Path
- Toggle Pin
- Open File Location
- Delete

Attach `Details` to the primary note row or action rows:

- `Details.Title`: note title
- `Details.Body`: Markdown body
- `Details.Metadata`: category, tags, created, updated, path, pinned state

### DeleteNotePage

Use a confirmation page instead of immediate delete:

- Row 1: Cancel
- Row 2: Delete Note, critical

Delete moves to Recycle Bin, never permanent delete.

## Provider Wiring

`NpuNotesExtension` should instantiate a real provider:

```csharp
private readonly NpuNotesCommandsProvider _provider = new();
```

`NpuNotesCommandsProvider` should:

- Set `Id = "com.local.nputools.notes"`.
- Set `DisplayName = "NPU Notes"`.
- Set `Icon` to a static visual helper icon.
- Instantiate `NotesSettingsManager`, `NotesIndexStore`, `NotesStore`, and `NotesSearchService` once.
- Assign `Settings = _settingsManager.Settings`.
- Return the top-level command list.

Do not keep using `Phase0CommandProvider` after the first implementation commit.

## Build Order

### Milestone 1: File-backed MVP

1. Add models, visuals, key chords, settings.
2. Add `NotesStore` with parse/create/load/delete-to-recycle-bin.
3. Add provider and top-level pages.
4. Add commands for open/copy/path/location/pin/delete.
5. Build and deploy.
6. Verify create -> file exists -> search -> preview -> open -> delete-to-recycle-bin.

### Milestone 2: Raycast polish

1. Improve browse categories.
2. Add pinned ordering.
3. Add recent-opened tracking.
4. Add create-from-clipboard.
5. Add details metadata and snippets.
6. Add settings for root/default category/open-after-create.

### Milestone 3: NPU features

1. Add `NotesAiService`.
2. Add optional AI cleanup on create.
3. Add Find Related Notes result page.
4. Add semantic fallback search only when keyword results are scarce.
5. Add caps for semantic checks/results.

### Milestone 4: Indexing and advanced search

1. Add explicit Rebuild Index command if needed.
2. Evaluate AppContentIndexer only after local search and Phi fallback are stable.
3. Consider RAG-style question answering over notes.

## Verification

Use the normal extension loop:

```powershell
Stop-Process -Name "NpuNotesExtension" -Force -ErrorAction SilentlyContinue
dotnet build "src\NpuNotesExtension\NpuNotesExtension.csproj" -p:Platform=x64
Add-AppxPackage -Register "src\NpuNotesExtension\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml" -ForceApplicationShutdown
```

Then reload Command Palette extensions and confirm the log line:

```text
Loaded N command(s) and 0 band(s) from NpuNotesExtension
```

Manual test checklist:

- Create blank note opens editor and creates a Markdown file.
- Create typed note uses first line as title.
- Category folder is created automatically.
- Search finds title matches.
- Search finds body matches.
- Empty search shows pinned and recent notes.
- Pin/unpin changes ordering.
- Open in editor works.
- Open file location works.
- Copy content copies only body content, not frontmatter.
- Delete moves to Recycle Bin.
- Imported Markdown without frontmatter appears in browse/search.
- Settings changes refresh pages without process restart.

Unit tests to add in `NpuTools.Tests`:

- Frontmatter parse/write round trip.
- Title fallback from first heading.
- Title fallback from first non-empty line.
- Slug filename generation and collision handling.
- Category normalization.
- Search ranking.
- Index pruning for missing files.
