# Bugs And Risks

This is the active issue ledger for ActionCenterExtension.

No bugs have been filed yet — the project is in initial scaffolding.

## Known Risks (Pre-Development)

### RISK-001: WinRT Wrapper Lifetime

Priority: High  
Area: WinRT lifetime  

The old WinUI 3 app had a recurring idle crash (`System.AccessViolationException` in `WinRT.IObjectReference.Finalize`) caused by short-lived WinRT wrappers for battery, network, and SMTC being finalized after the OS-side object was gone. This is a known .NET/WinRT interop hazard.

Mitigation: retain WinRT source objects (e.g., `Battery.AggregateBattery`, network connectivity objects, SMTC manager) as instance fields on the service for process lifetime. Do not let them be GC'd while subscriptions are active. This was the fix that stabilized the old app; apply the same pattern here from the start.

### RISK-002: Deploy vs Build Confusion

Priority: Medium  
Area: Developer workflow  

The extension is registered as an MSIX package. A plain `dotnet build` does not update the registered extension. Changes will appear to have no effect if the developer forgets to **Deploy** after building. This is easy to forget.

Mitigation: document clearly in RUNBOOK.md (done). Consider adding a README note at the repo root.

### RISK-003: Dock Band ID Missing

Priority: Medium  
Area: Dock integration  

Dock bands without a non-empty `Command.Id` are silently ignored by the PowerToys dock. There is no error; the band simply does not appear. This is a subtle footgun when adding new bands.

Mitigation: document in CONVENTIONS.md (done). Catch in code review.

### RISK-004: SDK Version Gate

Priority: Low  
Area: Build  

`GetDockBands()` and `WrappedDockItem` require SDK ≥ 0.9.260303001. If the `Directory.Packages.props` version is older, these types will not exist and the build will fail with confusing errors.

Mitigation: verify the SDK version before starting dock work. Document in RUNBOOK.md (done).
