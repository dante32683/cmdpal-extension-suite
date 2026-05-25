# NPU Obsidian Extension Guide

This is the implementation plan for adding an `NpuObsidianExtension` to this monorepo. The goal is to mimic the installed third-party Obsidian Command Palette extension first, then add NPU-native capabilities that match the patterns used by the other NPU extensions.

## Current Reference Extension

The installed reference package is:

- Package: `ObsidianExtension_0.0.5.0_x64__1ncszaap3xyd4`
- Display name: `Obsidian Notes`
- Provider ID in source/settings: `com.zadjii.obsidian-extension`
- Top-level command count in the local CmdPal log: 3 commands, 0 bands
- Public source: `https://github.com/zadjii/CmdPalExtensions/tree/main/src/extensions/ObsidianExtension`

The reference extension exposes:

| Command | Behavior |
|---|---|
| `Obsidian Notes` | Lists Markdown notes in one configured vault. |
| `New note` | Opens Obsidian via `obsidian://new?vault=...`. |
| `Open daily note` | Opens Obsidian via `obsidian://daily?vault=...`. |

Reference search/list behavior:

- A single `VaultPath` setting points to the Obsidian vault folder.
- If the vault path is unset or invalid, the page shows an `Open settings` row.
- The extension recursively scans `*.md` files.
- It skips `.git` and `.obsidian` directories.
- Notes sort by last modified time descending.
- Each result opens a preview page in Command Palette.
- Context actions include open in Obsidian, quick edit, and quick add.
- Open/new/daily actions use Obsidian URI schemes instead of direct process discovery.

## Product Shape

Build a new independent extension, not a forked package identity:

- Project: `src/NpuObsidianExtension`
- Package identity: `NpuObsidianExtension`
- Provider ID: `com.local.nputools.obsidian`
- Display name: `NPU Obsidian`
- Default app extension ID: `NpuObsidian`
- Commands in MVP: 5 top-level commands

Top-level commands:

| Command | Page/command type | MVP behavior |
|---|---|---|
| `Obsidian` | `ListPage` hub | Shows pinned/recent notes, quick actions, vault status, index status. |
| `Search Obsidian Notes` | `DynamicListPage` | Searches title, path, headings, tags, aliases, body snippets, and indexed AI summary. |
| `New Obsidian Note` | `DynamicListPage` or `FormContent` | Creates a Markdown note directly or opens Obsidian new-note URI. |
| `Open Daily Note` | `InvokableCommand` | Uses `obsidian://daily?vault=...`. |
| `Index Vault` | async `ListPage` | Builds/refreshes the vault index without blocking the COM thread. |

Keep the reference extension installed only while building. Once this extension is usable, uninstall the reference package so search results are not duplicated.

## MVP Feature Set

The first milestone should be local and deterministic:

1. Vault settings
   - `vaultPath`: required folder path.
   - `vaultName`: optional override; default to the last folder name.
   - `dailyNotesFolder`: optional relative path.
   - `templatesFolder`: optional relative path.
   - `defaultNewNoteFolder`: optional relative path.
   - `openCreatedNotesInObsidian`: toggle.
   - `maxSearchResults`: choice set.

2. Markdown vault store
   - Enumerate Markdown files under `vaultPath`.
   - Skip `.git`, `.obsidian`, `.trash`, and configured ignored folders.
   - Parse frontmatter with a tolerant parser.
   - Extract title from frontmatter title, first H1, then filename.
   - Extract tags from frontmatter, inline `#tags`, and `tags:` values.
   - Extract aliases from frontmatter.
   - Extract headings for details metadata and lightweight ranking.

3. Search
   - Keep an in-memory index loaded once from a JSON sidecar under `%LocalAppData%\NpuObsidian\vault-index.json`.
   - `Search Obsidian Notes` must extend `DynamicListPage` and filter on `UpdateSearchText`.
   - Rank exact title > alias > tag > heading > path > body > AI summary.
   - Empty query shows pinned and recently opened notes.

4. Preview and actions
   - Primary action: preview note in Command Palette.
   - Context actions: open in Obsidian, open in default editor, reveal in Explorer, copy Obsidian URI, copy Markdown link, quick append, quick edit.
   - Destructive actions are out of MVP unless explicitly requested.

5. Create note
   - Direct file creation is preferable to only launching `obsidian://new`, because it lets the NPU pipeline classify, template, and index immediately.
   - First line becomes the title by default, matching the pattern already used by `NpuNotesExtension`.
   - Slugify filenames with the same collision-safe file logic used in Notes/Organize patterns.
   - Optional URI action still exists as `Open Obsidian New Note`.

## NPU Features

Add NPU behavior after the deterministic MVP is stable:

1. AI summarize note
   - Use Phi `LanguageModel` to produce a short summary.
   - Store summaries in the local index, not in the note file by default.
   - Expose `Regenerate Summary` as a context action.

2. Find related notes
   - Candidate set: recent notes, same-folder notes, shared tags, backlink-neighbor notes, and title/body keyword hits.
   - Use deterministic scoring first.
   - Use Phi only to rerank a capped candidate set.
   - Show why each note matched: shared tags, backlinks, folder, heading, or semantic score.

3. Smart capture
   - User types rough text.
   - Phi proposes title, folder, tags, and cleaned Markdown body.
   - The user confirms before writing.
   - Fall back to deterministic title/folder when the model is unavailable.

4. Ask vault
   - Answer questions over a capped set of indexed notes.
   - Start with keyword retrieval and AI summaries.
   - Show source notes as result rows with snippets.
   - Do not attempt full-vault RAG until indexing and caps are proven.

5. Semantic fallback search
   - If keyword search returns too few results, ask Phi to score a capped set of candidate summaries.
   - Keep this opt-in or bounded by a setting to avoid slow searches.

## File Structure

Use the established monorepo pattern:

```text
src/NpuObsidianExtension/
  Program.cs
  NpuObsidianExtension.cs
  NpuObsidianCommandsProvider.cs
  KeyChords.cs
  ObsidianVisuals.cs
  Models/
    ObsidianNote.cs
    ObsidianVaultSettings.cs
    ObsidianVaultIndex.cs
    ObsidianSearchResult.cs
  Services/
    ObsidianSettingsStore.cs
    ObsidianSettingsManager.cs
    ObsidianVaultStore.cs
    ObsidianIndexStore.cs
    ObsidianSearchService.cs
    ObsidianUriService.cs
    ObsidianAiService.cs
  Pages/
    ObsidianHubPage.cs
    SearchObsidianNotesPage.cs
    NotePreviewPage.cs
    CreateObsidianNotePage.cs
    QuickAppendPage.cs
    QuickEditPage.cs
    IndexVaultPage.cs
  Commands/
    OpenObsidianNoteCommand.cs
    OpenDailyNoteCommand.cs
    OpenObsidianNewNoteCommand.cs
    OpenNoteLocationCommand.cs
    CopyObsidianLinkCommand.cs
  Shared/
    ObsidianJsonContext.cs
```

Service rules:

- Services do not reference Command Palette SDK types.
- `CommandProvider` instantiates each service once and injects it into pages/commands.
- JSON uses source generation for AOT compatibility.
- File-backed state uses atomic temp-file writes.
- Long-running vault index and AI work use async result pages with `Interlocked` lazy start.

## Index Design

Index path:

```text
%LocalAppData%\NpuObsidian\vault-index.json
```

Index entry fields:

- `absolutePath`
- `relativePath`
- `title`
- `aliases`
- `tags`
- `headings`
- `links`
- `backlinks`
- `bodySnippet`
- `aiSummary`
- `lastModifiedUtc`
- `indexedUtc`
- `contentHash`
- `lastOpenedUtc`
- `isPinned`

Indexing should:

1. Load existing index at startup.
2. Enumerate Markdown files on explicit `Index Vault`.
3. Skip unchanged files by path + modified timestamp + optional content hash.
4. Parse changed files.
5. Build outbound wiki links and Markdown links.
6. Build backlinks after all files are parsed.
7. Save atomically.

Do not add a background file watcher in v1. A manual rebuild is easier to verify and avoids process-lifetime confusion. A watcher can be a later feature if the manual path is stable.

## Obsidian URI Rules

Prefer URI commands for actions that should land inside Obsidian:

```text
obsidian://open?vault=<vaultName>&file=<relativePath>
obsidian://new?vault=<vaultName>
obsidian://daily?vault=<vaultName>
```

Always escape vault names and relative paths with `Uri.EscapeDataString`. Keep URI construction in `ObsidianUriService`.

## Relationship To NPU Notes

`NpuNotesExtension` and `NpuObsidianExtension` should stay separate:

- NPU Notes owns a lightweight standalone Markdown note store under `%UserProfile%\Documents\NpuNotes`.
- NPU Obsidian owns an existing Obsidian vault, respects Obsidian folder/link conventions, and opens deep links with Obsidian URIs.
- Shared logic can be extracted later only if both implementations need the same parser/search helpers.

Do not prematurely move Notes services into `NpuTools.Shared`. Duplicate a small amount first, then extract only proven common pieces.

## Test Plan

Unit tests in `NpuTools.Tests`:

- Markdown frontmatter parsing.
- Title fallback order.
- Tag parsing from frontmatter and inline tags.
- Wiki-link and Markdown-link extraction.
- Folder ignore rules.
- Search ranking.
- URI escaping.
- Collision-safe note filename creation.

Manual verification:

1. Stop `NpuObsidianExtension`.
2. Build with `dotnet build "src\NpuObsidianExtension\NpuObsidianExtension.csproj" -p:Platform=x64`.
3. Register with `Add-AppxPackage -Register "src\NpuObsidianExtension\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml" -ForceApplicationShutdown`.
4. Reload Command Palette extensions.
5. Confirm the log shows 5 commands and 0 bands from `NpuObsidianExtension`.
6. Test against one small vault before indexing a large vault.
7. For AI features, test one note first, then a capped folder, then the full vault.

## Milestones

### Milestone 1: Feature-Parity MVP

- Scaffold `NpuObsidianExtension`.
- Add vault settings.
- List Markdown notes.
- Preview notes.
- Open note/new note/daily note via Obsidian URI.
- Quick edit and quick append.
- Build and deploy successfully.

### Milestone 2: Local Index And Better Search

- Add persistent index.
- Add `Index Vault` async page.
- Add title/tag/alias/heading/body search ranking.
- Add pinned/recent metadata.
- Add tests for parser and ranking.

### Milestone 3: NPU Layer

- Add note summaries.
- Add smart capture.
- Add find-related notes.
- Add semantic fallback search.

### Milestone 4: Vault Intelligence

- Add backlinks and graph-neighbor ranking.
- Add Ask Vault over retrieved sources.
- Add optional AppContentIndexer exploration only after the local index is proven.

## Open Decisions

- Whether to remove the existing third-party `ObsidianExtension` before or after `NpuObsidianExtension` reaches MVP.
- Whether direct note creation should write into a default folder or always prompt/select a folder.
- Whether quick edit should overwrite the full file or expose safer append/prepend actions first.
- Whether AI summaries should ever be written into frontmatter, or always remain in the local sidecar index.
