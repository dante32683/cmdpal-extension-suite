# Agent Orientation

This is the canonical entry point for all AI agents working in this repo. All other AI instruction files (`CLAUDE.md`, `.github/copilot-instructions.md`) point here.

## Read the docs first

Read these in order before writing any code:

1. `docs/README.md` — what this repo contains
2. `docs/CONTEXT.md` — why extensions live together; key SDK gotchas (GetItems blocking, COM lifetime)
3. `docs/ARCHITECTURE.md` — file layout, SDK primitives, async patterns with code examples
4. `docs/RUNBOOK.md` — build/deploy loop, log reading, common failures, AI agent onboarding
5. `docs/CONVENTIONS.md` — naming, icons, async rules, git workflow, AI search pattern, verification steps
6. `docs/BUGS.md` — known issues and resolved bugs; do not re-introduce these
7. `docs/ROADMAP.md` — what is implemented and what is planned next

Do not treat `docs/extensions/` archived notes as current truth unless a canonical doc above points to them.

## Pre-flight rules (follow before every change)

**1. Branch first.** Every non-trivial change goes on a branch. Never commit directly to `main` except single-line typo fixes.

```powershell
git checkout -b feat/short-description   # new feature or capability
git checkout -b fix/short-description    # bug fix
git checkout -b chore/short-description  # refactor, deps, docs-only
```

**2. Stop the extension process before building.** The output `.exe` is locked while the extension runs. Building without stopping it fails with `MSB3021`.

```powershell
Stop-Process -Name "NpuOrganizeExtension" -Force -ErrorAction SilentlyContinue
# also stop any companion daemon, e.g.:
Stop-Process -Name "NpuOrganizeKeeper" -Force -ErrorAction SilentlyContinue
```

**3. Deploy after every build.** `dotnet build` alone does not reload the extension in PowerToys.

```powershell
dotnet build "src\[ExtensionName]\[ExtensionName].csproj" -p:Platform=x64
Add-AppxPackage -Register "src\[ExtensionName]\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml" -ForceApplicationShutdown
# then in Command Palette: "Reload Command Palette extensions"
```

**4. Check the log after deploying.** Confirm the extension loaded with the expected command count before testing anything.

```powershell
$log = "$env:LOCALAPPDATA\Microsoft\PowerToys\CmdPal\Logs"
$ver = Get-ChildItem $log | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content "$($ver.FullName)\Log_$(Get-Date -Format 'yyyy-MM-dd').log" | Select-String "NpuOrganize|Loaded|Started" | Select-Object -Last 10
```

Look for: `Loaded N command(s) and M band(s) from [ExtensionName]` — N must match the length of your `TopLevelCommands()` array.

**5. Test AI features one file at a time.** Features using `ImageDescriptionGenerator`, `OcrEngine`, or `LanguageModel` cannot be unit-tested outside the MSIX+COM host. Trigger the pipeline on one file first and verify the result before running any bulk operation.

**6. Update the docs.** When you establish a new pattern or discover something non-obvious, update the smallest relevant doc in the same session. The docs are the persistent memory across agent sessions.
