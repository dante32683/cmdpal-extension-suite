# Bugs And Known Risks

This is the active issue ledger.

No bugs filed yet.

## Known Risks

### RISK-001: WinRT Wrapper Lifetime

Priority: High
Area: WinRT lifetime

WinRT wrappers for battery, network, and SMTC can cause `AccessViolationException` in `WinRT.IObjectReference.Finalize` if they are GC'd while the OS-side object is still active. This is a known .NET/WinRT interop hazard.

Mitigation: retain WinRT source objects as instance fields on the service for process lifetime. Unsubscribe all events in `Dispose`.

### RISK-002: Deploy vs Build Confusion

Priority: Medium
Area: Developer workflow

`dotnet build` alone does not register the extension. Changes will appear to have no effect if the deploy step is skipped.

Mitigation: always run all three steps in the dev loop (see RUNBOOK.md). The deploy step is `Add-AppxPackage -Register ...`.

### RISK-003: Dock Band ID Missing

Priority: Medium
Area: Dock integration

Dock bands without a non-empty `Command.Id` are silently ignored. There is no error.

Mitigation: set `Id` in every command constructor. Verify in code review.

### RISK-004: Dock Band Toggle Re-Opens Instead Of Closing

Priority: Medium
Area: Dock UX

When a dock button opens a panel and the user clicks again, the Command Palette focus shift closes the panel before `Invoke()` runs — then `Invoke()` re-opens it. The result is that clicking a second time re-opens instead of closing.

Mitigation: use the state-toggle pattern with an auto-reset timer. See ARCHITECTURE.md.
