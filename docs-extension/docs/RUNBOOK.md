# Runbook

## Requirements

- Windows 10 1809 or newer (Windows 11 recommended for dock feature).
- .NET 8 SDK.
- PowerToys v0.98.0 or newer (Command Palette dock support).
- Visual Studio 2022 with the Windows App SDK workload (for MSIX deploy).
- `Microsoft.CommandPalette.Extensions` SDK ≥ 0.9.260303001 (check `Directory.Packages.props`).

## Build

```powershell
dotnet build ActionCenterExtension\ActionCenterExtension.csproj
```

A plain build verifies the code compiles but does not register the extension with PowerToys.

## Deploy (Required To See Changes In PowerToys)

In Visual Studio:

1. Open `ActionCenterExtension.sln`.
2. **Build > Deploy Solution** (or right-click the project → Deploy).

This registers the MSIX package with the OS. Plain build/publish does not register it.

After deploying, trigger a reload in Command Palette:

```
Command Palette → type "Reload" → select "Reload Command Palette extensions"
```

The extension will appear in the palette and, if `GetDockBands()` is implemented, in the dock.

## Debug

Run the project with F5 in Debug configuration. The COM server launches and attaches to the debugger. Output goes to the Output window (Ctrl+Alt+O).

To see extension log output:
- Check the Output window in Visual Studio while running in Debug.
- PowerToys itself logs extension activation failures to `%LocalAppData%\Microsoft\PowerToys\logs`.

## Publish (Release Build)

Use the publish profiles under `Properties/PublishProfiles/`:

```powershell
dotnet publish ActionCenterExtension\ActionCenterExtension.csproj -p:PublishProfile=win-x64
```

After publishing, deploy the resulting MSIX or use the publish profile's deployment step.

## Common Failures

### Extension Does Not Appear In Palette

- Verify you used **Deploy**, not just Build.
- Run the `Reload` command in Command Palette.
- Check `%LocalAppData%\Microsoft\PowerToys\logs` for activation errors.
- Confirm the GUID in `ActionCenterExtension.cs` matches what is registered (check `Package.appxmanifest`).

### Dock Band Does Not Appear

- Confirm `GetDockBands()` is overridden on the `CommandProvider`.
- Confirm every returned `ICommandItem` has a `Command` with a non-empty `Id`.
- Confirm the `CommandProvider` itself has a non-empty `Id` set.
- Run the `Reload` command after deploy.
- PowerToys dock must be enabled in PowerToys settings.

### Build Errors About Missing SDK Types

- Confirm `Microsoft.CommandPalette.Extensions` version is ≥ 0.9.260303001 in `Directory.Packages.props`.
- `GetDockBands` and `WrappedDockItem` are only available in SDK 0.9+.

### COM Activation Fails

- Do not modify the COM server registration pattern in `Program.cs`.
- The process must be launched with `-RegisterProcessAsComServer` by the PowerToys host — do not launch it manually for production use (F5 debug launch is fine).

## Source Control Notes

The scaffold's `.gitignore` excludes `Properties/launchSettings.json` and `*.pubxml` by default. These files are needed for deployment. Either remove those exclusions from `.gitignore` or commit the files with `git add --force`.
