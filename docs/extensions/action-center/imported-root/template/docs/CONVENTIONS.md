# Conventions

## Code Style

- 4-space indentation.
- `PascalCase` for types and public members.
- `_camelCase` for private fields.
- `camelCase` for locals and parameters.
- Prefer SDK toolkit helpers (`IconHelpers`, `CommandItem`, `ListItem`, `WrappedDockItem`) over manual interface implementations.
- Comments explain non-obvious platform behavior only — not routine code.

## File Structure

- One band or command per file under `Bands/`.
- One service per domain under `Services/`. Services do not reference the SDK — they expose events or properties that bands consume.
- All P/Invoke declarations under `Interop/`, one file per DLL (e.g., `User32.cs`, `Dwmapi.cs`).
- `[ExtensionName]CommandsProvider.cs` wires everything together — no business logic here.
- Do not add behavior to `Program.cs` or `[ExtensionName].cs` — they are infrastructure only.

## Naming

- Band/command classes: `<Feature>Command.cs` (e.g., `QuickSettingsCommand.cs`).
- Service classes: `<Feature>Service.cs` (e.g., `BatteryService.cs`).
- Pages: `<Feature>Page.cs` (e.g., `SettingsPage.cs`).

## IDs

- Provider ID: `"com.<yourname>.<extensionname>"` — set in the `CommandProvider` constructor.
- Command ID: `"com.<yourname>.<extensionname>.<commandname>"` — set in each command constructor.
- IDs must be non-empty. Missing IDs cause silent failures in the dock.

## Icons

- Always use explicit Unicode escapes for glyphs: `new IconInfo("")`.
- Do not paste glyph characters directly into source — they can be silently corrupted.
- The default icon font is Segoe Fluent Icons. Verify codepoints against that font, not Segoe MDL2 Assets (they overlap but are not identical).
- Use `IconHelpers.FromRelativePath("Assets\\MyIcon.png")` for file-based icons.

## SendInput vs keybd_event

- Use `SendInput` for synthetic keypresses. `keybd_event` is deprecated.
- Include `MOUSEINPUT` in the `INPUTUNION` struct so the union is the correct size (40 bytes on x64). Omitting it causes `SendInput` to silently reject calls.
- Use `[LibraryImport]` (source-generated) over `[DllImport]` for all P/Invoke declarations.

## Error Handling

- WinRT callbacks and timer handlers must have top-level exception guards. Unhandled exceptions in these contexts crash the COM server.
- Do not swallow exceptions silently.
- Retain WinRT source objects (battery, network, SMTC) as instance fields for process lifetime. Short-lived WinRT wrappers that are GC'd while the OS-side object is active cause `AccessViolationException` in `WinRT.IObjectReference.Finalize`.
- Unsubscribe all WinRT events and cancel timers in `Dispose`.

## Git

- `main` — stable, deployable code.
- `feature/...` — new bands or features.
- `fix/...` — bug fixes.
- Commits are imperative and focused (e.g., `Add QuickSettings dock band`).
- Do not commit `bin/` or `obj/`.
- Commit `Properties/launchSettings.json` and `*.pubxml` — they are needed for deployment.

## Docs

- `ARCHITECTURE.md` — current truth for code shape. Update when the file structure or SDK usage changes.
- `RUNBOOK.md` — operational commands. Update when the build/deploy steps change.
- `CONVENTIONS.md` — standards. Update when a new pattern is established.
- `BUGS.md` — active issues. Add on discovery, resolve when fixed.
- `ROADMAP.md` — planned work. Update as features are completed or reprioritized.
- Update the smallest relevant doc when something changes — do not leave stale docs.
