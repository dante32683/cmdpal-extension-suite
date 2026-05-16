# Conventions

## Code Style

- Use 4-space indentation.
- Use `PascalCase` for types and public members.
- Use `_camelCase` for private fields.
- Use `camelCase` for locals and parameters.
- Prefer SDK toolkit helpers (`IconHelpers`, `CommandItem`, `ListItem`, `WrappedDockItem`) over manual interface implementations where they cover the need.
- Keep comments short and explain non-obvious platform behavior, not routine code.

## Architecture

- One band per file under `Bands/`. Each band owns its own timer, WinRT subscription, and disposal.
- One service per domain under `Services/`. Services do not know about the SDK or dock; they expose events or properties that bands consume.
- P/Invoke declarations live under `Interop/`, one file per DLL.
- `ActionCenterExtensionCommandsProvider.cs` wires everything together. It should not contain business logic.
- Do not add behavior to `Program.cs` or `ActionCenterExtension.cs` — they are infrastructure only.

## Naming

- Band classes: `<Feature>Band.cs` (e.g., `ClockBand`, `BatteryBand`).
- Service classes: `<Feature>Service.cs` (e.g., `BatteryService`, `NetworkService`).
- Command classes: `<Action>Command.cs` (e.g., `QuickSettingsCommand`).

## Error Handling

- WinRT callbacks and timer elapsed handlers must have top-level exception guards. Unhandled exceptions in these contexts will crash the COM server process.
- Do not swallow exceptions silently. Log them or let them propagate to the guard.
- Treat WinRT wrapper lifetimes carefully — the old app had a class of idle crash from short-lived WinRT wrappers being finalized after the OS-side object was torn down. Retain event subscriptions and source objects as long as the band is alive.
- Unsubscribe all WinRT events and cancel timers in `Dispose` / when the band is no longer needed.

## Interop

- Use `SendInput` for synthetic keypresses (Win+A for Quick Settings). Do not use `keybd_event` (deprecated).
- Keep all `DllImport` / `LibraryImport` declarations in `Interop/User32.cs` (or the appropriate DLL file).
- Use `[LibraryImport]` (source-generated) over `[DllImport]` where possible (.NET 7+ preference).

## Git

- Use `main` for stable, deployable code.
- Use `feature/...` branches for new bands or features.
- Use `fix/...` branches for bug fixes.
- Keep commits focused and imperative (e.g., `Add QuickSettings dock band`).
- Do not commit `bin/`, `obj/`, or local diagnostics.
- Commit `Properties/launchSettings.json` and `*.pubxml` — they are needed for deploy (remove the default `.gitignore` exclusions).

## Docs

- `docs/ARCHITECTURE.md` is current truth for code shape.
- `docs/RUNBOOK.md` is operational commands and debugging.
- `docs/ROADMAP.md` is live planned work.
- `docs/BUGS.md` is the active issue ledger.
- `docs/CONTEXT.md` is orientation background — do not treat it as current truth for code shape; the code evolves but CONTEXT.md is mostly stable historical background.
- Update the smallest relevant doc when behavior, workflow, or risk changes.
