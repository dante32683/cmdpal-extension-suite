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

Current extension process names:

- `ActionCenterExtension`
- `SimpleAnalyticsExtension`
- `NpuAwakeExtension`
- `NpuOrganizeExtension`
- `NpuImageEditorExtension`
- `NpuTextToolsExtension`
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
