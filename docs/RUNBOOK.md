# Runbook

## Requirements

- Windows 11.
- .NET 9 SDK or newer.
- PowerToys v0.98.0 or newer.
- `Microsoft.CommandPalette.Extensions` SDK v0.9.260303001 or newer.

## Solution Build

Build every project in the monorepo:

```powershell
dotnet build NpuCommandPaletteExtensions.sln -p:Platform=x64
```

The Command Palette SDK can emit packaging warning `APPX1707` for winmd references. That warning is known and does not block development builds.

## PowerToys Reference Checkout

The monorepo keeps an ignored sparse clone of PowerToys at:

```text
references\PowerToys
```

It is intended for local reference only. The checked-out path is `src/modules/cmdpal`, which includes Microsoft-maintained extensions such as:

```text
references\PowerToys\src\modules\cmdpal\ext\Microsoft.CmdPal.Ext.TimeDate
```

Refresh it manually when needed:

```powershell
git -C references\PowerToys pull
```

If Git reports a dubious ownership warning from the sandbox, run the refresh from the normal user shell or mark the checkout as safe for that user.

## Per-Extension Dev Loop

Use this after changing one extension:

```powershell
Stop-Process -Name "[ExtensionName]" -Force -ErrorAction SilentlyContinue
dotnet build "src\[ExtensionName]\[ExtensionName].csproj" -p:Platform=x64
Add-AppxPackage -Register "src\[ExtensionName]\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml" -ForceApplicationShutdown
```

Then in Command Palette:

```text
Reload Command Palette extensions
```

## Refresh Existing Local Registrations

After moving projects or consolidating old repos into this monorepo, refresh local package registrations from the monorepo paths:

```powershell
.\scripts\Refresh-ExtensionRegistrations.ps1
```

After verifying Command Palette loads the monorepo registrations, move the old sibling folders to the Recycle Bin:

```powershell
.\scripts\Refresh-ExtensionRegistrations.ps1 -MoveOldFoldersToRecycleBin
```

The script stops extension processes, removes matching app packages for the current user, registers the `src\...\AppxManifest.xml` files produced by the x64 Debug build, and optionally recycles:

- `C:\Portable\ActionCenterExtension`
- `C:\Portable\SimpleAnalyticsExtension`

Current extension process names:

- `ActionCenterExtension`
- `TimeDateDockExtension`
- `MediaControlsExtension`
- `SimpleAnalyticsExtension`
- `NpuAwakeExtension`
- `NpuOrganizeExtension`
- `NpuImageEditorExtension`
- `NpuTextToolsExtension`
- `NpuClipboardExtension`
- `NpuClipboardKeeper`
- `NpuNotesExtension`
- `NpuDevToolboxExtension`

## Awake Daemon

`NpuAwakeExtension` depends on `tools\NpuAwakeKeeper`. Building Awake copies `NpuAwakeKeeper.exe` into the Awake package output under `Tools\`.

Stop the daemon during development:

```powershell
Stop-Process -Name "NpuAwakeKeeper" -Force -ErrorAction SilentlyContinue
```

Awake runtime files live under:

```text
%LocalAppData%\NpuTools\Awake
```

## Publish

Publish one extension:

```powershell
dotnet publish "src\[ExtensionName]\[ExtensionName].csproj" -p:PublishProfile=win-x64
```

Publish artifacts are per extension. GitHub releases should attach one MSIX/artifact per independently installable extension.

## Git Workflow

- The monorepo uses one `main` branch.
- Keep extension work in focused branches such as `feature/awake-schedules` or `fix/action-center-toggle`.
- Do not create long-lived per-extension branches.
- Do not commit `bin/` or `obj/`.
- Preserve imported history by keeping old repo histories merged or tagged in this monorepo; do not keep nested `.git` directories under `src/`.

Imported histories:

- `imported/action-center` tag points at the old ActionCenter repo head that was merged into this monorepo as history.
- Simple Analytics had no `.git` repository at migration time, so only its files and docs were imported.

## Starting A New AI Agent Session

The monorepo has a `CLAUDE.md` file at the repo root that Claude Code reads automatically at the start of every session. It points to the docs in the correct read order. Any AI agent that opens this repo will have that context injected before your first message.

To get a fresh agent fully oriented, open a new chat in this working directory and say:

```
Read the docs in the order specified in CLAUDE.md before we start.
```

The agent will then read:

1. `docs/README.md` — what the repo contains
2. `docs/CONTEXT.md` — why extensions live together, key gotchas (GetItems blocking, COM lifetime)
3. `docs/ARCHITECTURE.md` — file layout, SDK primitives, async patterns
4. `docs/RUNBOOK.md` — build/deploy loop, log reading, common failures
5. `docs/CONVENTIONS.md` — naming, icons, async rules, git workflow, AI search pattern, verification steps
6. `docs/BUGS.md` — known issues and resolved bugs to avoid re-introducing
7. `docs/ROADMAP.md` — what is implemented and what is planned

After reading, the agent will know the branch naming convention, the deploy loop, how to read the log, how to verify AI features, and all established patterns. You should not need to re-explain any of this.

### Keeping docs current

The agent is only as good as the docs. When you establish a new pattern, fix a recurring mistake, or discover something non-obvious about the SDK or tooling, update the smallest relevant doc in that same session. The docs are the persistent memory across agent sessions.

---

## Reading The Log

The Command Palette host writes a rolling log at:

```text
%LocalAppData%\Microsoft\PowerToys\CmdPal\Logs\{version}\Log_{date}.log
```

Read the tail in PowerShell:

```powershell
$log = "$env:LOCALAPPDATA\Microsoft\PowerToys\CmdPal\Logs"
$latest = Get-ChildItem $log | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content "$($latest.FullName)\Log_$(Get-Date -Format 'yyyy-MM-dd').log" | Select-Object -Last 60
```

Or grep for extension-specific lines:

```powershell
Get-Content $logFile | Select-String -Pattern "(NpuOrganize|error|Started|Loaded)" | Select-Object -Last 30
```

### Key patterns to look for

| Pattern | What it means |
|---|---|
| `Started extension FooExtension_... in N ms` | Extension process launched and responded to COM activation |
| `Loaded N command(s) and M band(s) from FooExtension` | Extension's `TopLevelCommands()` and `GetDockBands()` returned successfully |
| `Loaded N command(s) ... from K extension(s) in N ms` | Full palette reload completed — total counts across all extensions |
| `Failed to find band com.dziad...` | A previously pinned dock band is not available — normal during partial reloads |
| `SearchQuery TryExecuteFallbackQuery` | Host's own file-system search found nothing for the typed string — not an extension error |

### Verifying your extension loaded

After `Reload Command Palette extensions`, find your extension's load line and confirm the command count matches the length of your `TopLevelCommands()` array:

```text
Loaded 6 command(s) and 0 band(s) from NpuOrganizeExtension_0.0.1.0_x64__8wekyb3d8bbwe in 63 ms
```

If the count is wrong or the line is missing, the extension process crashed during startup — check for exceptions above that line.

### Noise to ignore

**`error fast initializing CommandItemViewModel` / `COMException -2147023174 (RPC_S_SERVER_UNAVAILABLE)`** — this fires when the host tries to read a command ID from a COM server that is no longer running. It almost always comes from OTHER extensions whose processes have died (e.g. the old version of your extension that was just killed before the reload, or a different extension that is not registered in the current session). If your extension's `Loaded N command(s)` line appears cleanly and N is correct, these errors are not yours.

The reliable signal for your extension crashing is: no `Started extension` line, or an unhandled exception in the log between `Starting extension` and `Loaded`.

### Tailing the log live

To watch new entries appear during a test session:

```powershell
$log = "path\to\Log_yyyy-MM-dd.log"
$pos = (Get-Item $log).Length
Start-Sleep -Seconds 10   # do something in the palette during this window
$content = Get-Content $log -Raw
$content.Substring([int]$pos)
```

---

## Common Failures

### Extension Does Not Appear

- Confirm the extension was registered with `Add-AppxPackage -Register`.
- Run the Command Palette reload command.
- Check `%LocalAppData%\Microsoft\PowerToys\CmdPal\Logs\`.
- Verify the COM GUID in `[ExtensionName].cs` matches `Package.appxmanifest`.

### Dock Band Does Not Appear

- Confirm `GetDockBands()` returns a command item.
- Confirm the provider and dock command/page have non-empty IDs.
- Remove duplicate stale dock pins, then reload the extension.
- Confirm the PowerToys dock is enabled.

### Build Fails After Moving Projects

- Build through `NpuCommandPaletteExtensions.sln`.
- Confirm project references use the new `src/` and `tools/` paths.
- Confirm central versions are in `Directory.Packages.props`.
- Delete stale `bin/` and `obj/` folders, then rebuild.

### Dock Button Reopens Instead Of Closing

Use the state-toggle workaround. Do not rely on Win32 window detection for Windows 11 Quick Settings or other system UI.

### Build Fails With MSB3021 / MSB3027 (File Locked)

```text
error MSB3021: Unable to copy file "...NpuOrganizeExtension.exe"...
The process cannot access the file because it is being used by another process.
```

The extension process is still running and has the output exe locked. Stop it first:

```powershell
Stop-Process -Name "NpuOrganizeExtension" -Force -ErrorAction SilentlyContinue
```

If the extension has a companion daemon (e.g. `NpuOrganizeKeeper`), stop that too before building.

Always use PowerShell (`Stop-Process`, `Add-AppxPackage`) for process and package management on Windows — these cmdlets are not available in a bash shell.

### Testing AI Features Incrementally

AI pipeline features (`ImageDescriptionGenerator`, `OcrEngine`, `LanguageModel`) cannot be unit-tested outside the MSIX+COM context. Test them in stages:

1. **One file first.** Before triggering any bulk operation (Index All, Rename All), run the same pipeline on a single file through the normal UI. Confirm the result is correct and the index or output is updated. A bug in the pipeline will surface immediately without processing hundreds of files.
2. **Check the index/output file.** For index-backed features, verify the backing file (`%LocalAppData%\NpuOrganize\index.json`, etc.) was created and contains the expected fields after the single-file test.
3. **Then bulk.** Once the single-file round-trip is confirmed, the bulk operation is just that same path repeated — run it confidently.
