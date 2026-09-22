# Agent Orientation

This is the root guidance for AI agents working in this repository. `.github/copilot-instructions.md` points here. A nearer `AGENTS.md`, if one is added later, overrides this file for its subtree.

## Start here

Before editing:

1. Run `git status --short --branch`. Preserve existing user changes and never use destructive Git commands to clear a dirty tree.
2. For any non-trivial change, create a focused `feat/`, `fix/`, or `chore/` branch. Do not commit directly to `main` except for a zero-risk, single-line documentation correction.
3. Read the canonical documentation in this order:
   - `docs/README.md` — repository map
   - `docs/CONTEXT.md` — project rationale and SDK constraints
   - `docs/ARCHITECTURE.md` — structure and async patterns
   - `docs/RUNBOOK.md` — build, deploy, and diagnostics
   - `docs/CONVENTIONS.md` — implementation standards
   - `docs/BUGS.md` — known and resolved defects
   - `docs/ROADMAP.md` — current and planned scope

Treat `docs/extensions/` as archived history unless a canonical document explicitly points to a file there.

## Non-negotiable engineering rules

### Keep the Command Palette responsive

- Constructors, `GetItems()`, `UpdateSearchText()`, and timer callbacks must stay cheap. Do not perform recursive enumeration, bulk file reads, AI work, `.Wait()`, `.Result`, or `.GetAwaiter().GetResult()` on the COM apartment thread.
- For filesystem-backed lists, expose an immutable in-memory snapshot, refresh it on `Task.Run`, show a loading row on first use, and call `RaiseItemsChanged()` when ready.
- For long work initiated from `Invoke()`, return immediately and run the operation asynchronously, or model the result as a `ListPage` whose first `GetItems()` starts the work.
- Cache icons. Never allocate `IconInfo` objects on each render, search update, or timer tick.
- Catch expected WinRT, process-launch, and filesystem failures at command boundaries and give the user actionable feedback. Do not silently swallow failures that leave the UI looking successful.

### Preserve identity and lifecycle correctness

- Every provider, package, COM server, command, page, and independently addable dock band needs a stable, unique ID. Do not use `GetHashCode()` for persisted identity.
- Treat a published ID as user data because pins and settings may reference it. If an ID must change, document the migration impact.
- Instantiate domain services once in the command provider and inject them. Subscribe settings-backed pages and bands to changes so updates do not require a host restart.
- Dispose timers and event subscriptions when their owning SDK type supports disposal.
- Companion keepers must be single-instance, synchronize cross-process file access, and write state atomically.

### Protect user data and test real code

- Tests must never read, move, delete, or overwrite production files under `%LocalAppData%`, a real notes directory, or a real Obsidian vault. Inject storage paths and use a unique temporary directory that the test owns.
- Test production implementations directly. Do not copy algorithms into test-only helper classes merely to make them testable; extract pure production code instead.
- Normalize and validate persisted settings on read. Use atomic replacement for shared JSON or state files.
- Never add secrets, machine-specific absolute paths, generated build output, or personal content to the repository.

### Keep scope and documentation honest

- Prefer the smallest cohesive change. Ask before adding a production dependency, changing package identity/publisher/version, or deleting user data.
- When a change establishes a pattern or invalidates documentation, update the smallest canonical document in the same change.
- Keep detailed explanations in `docs/`; keep this file concise and enforceable.

## Verification

Run the repository gate for normal changes:

```powershell
.\scripts\check.ps1
```

The gate restores packages, audits transitive dependencies, formats/verifies code, validates repository and UX conventions, builds into `.artifacts/check`, runs x64 tests, and checks authoritative docs. It does **not** deploy or overwrite the registered extension binaries, so running extensions do not need to be stopped first.

For changes involving project files, packaging, interop, native binaries, or architecture-specific code, also compile ARM64:

```powershell
.\scripts\check.ps1 -Runtime win-arm64 -Platform ARM64 -BuildOnly
```

For interactive host validation only:

1. Stop the exact extension process and any companion keeper whose normal `bin` output you are about to rebuild.
2. Build that extension into its normal output directory.
3. Register its generated `AppxManifest.xml`, then reload Command Palette extensions.
4. Inspect the current Command Palette log and confirm the provider loaded with the expected command and band counts.
5. Exercise the changed command, search, settings, or dock behavior. Test AI features on one file before any bulk run.

Do not deploy after an isolated quality-gate build. A successful `dotnet build` alone also does not prove that the host loaded or exercised the extension.

Report the checks you ran, any skipped interactive validation, and material warnings or uncertainty.

## Review checklist

During review, flag:

- synchronous work at Command Palette/COM boundaries;
- filesystem scans started by constructors or per-keystroke search;
- tests that touch real user state or duplicate production logic;
- unstable or duplicate IDs and package/COM identity mismatches;
- non-atomic shared state, missing single-instance guards, or unsynchronized keeper/extension access;
- swallowed exceptions, stale settings UI, repeated icon allocation, or timers/event handlers without lifecycle ownership;
- new warnings, unaudited dependencies, broken docs, or bypasses of `scripts/check.ps1`.

Explain the user-visible risk and point to the established safe pattern in `docs/ARCHITECTURE.md`, `docs/CONVENTIONS.md`, or `docs/RUNBOOK.md`.

## Optional Gemini helper

Use the bounded Gemini helper only when the user requests Gemini or a separate advisory pass:

```powershell
.\tools\gemini-subagent.ps1 -Mode Review -Prompt "Review the current git diff for likely bugs. Return only concrete findings."
```

Treat its output as advisory and verify every claim locally before changing code.
