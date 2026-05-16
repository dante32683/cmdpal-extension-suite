# Bugs And Known Risks

This is the active issue ledger for the monorepo.

No open bugs filed yet.

## Known Risks

### RISK-001: WinRT Wrapper Lifetime

Priority: High
Area: WinRT lifetime

WinRT wrappers can cause `AccessViolationException` in `WinRT.IObjectReference.Finalize` if they are garbage-collected while OS-side objects are still active.

Mitigation: retain WinRT source objects as instance fields for process lifetime. Unsubscribe events and cancel timers when deterministic disposal is available.

### RISK-002: Build vs Deploy Confusion

Priority: Medium
Area: Developer workflow

`dotnet build` does not register or reload an extension.

Mitigation: use the full build, `Add-AppxPackage -Register`, and Command Palette reload loop in `RUNBOOK.md`.

### RISK-003: Identity Collisions

Priority: Medium
Area: Packaging

Multiple extension projects in one repo can accidentally share provider IDs, COM GUIDs, or MSIX identities.

Mitigation: every installable extension keeps unique IDs. Review `Package.appxmanifest`, `[ExtensionName].cs`, and `CommandProvider.Id` when adding or copying projects.

### RISK-004: Stale Dock Pins

Priority: Medium
Area: Dock integration

Renaming or removing dock band IDs can leave stale pinned entries.

Mitigation: remove stale dock pins manually, reload Command Palette extensions, and keep dock command IDs stable once released.

### RISK-005: Git History Import Clarity

Priority: Low
Area: Repository migration

Old repos may have history that does not line up perfectly with the new `src/` paths.

Mitigation: merge old histories into the monorepo so commits remain reachable, and document any imported histories in `RUNBOOK.md` or migration commits.
