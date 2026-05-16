# Runbook

## Requirements

- Windows 11 (recommended).
- .NET 8 SDK or newer.
- PowerToys v0.98.0 or newer (Command Palette dock support).
- `Microsoft.CommandPalette.Extensions` SDK ≥ 0.9.260303001 (check `Directory.Packages.props`).

## Dev Loop (Build → Deploy → Reload)

Visual Studio is not required. Run these three commands after every change:

```powershell
# 1. Kill the running extension process so the binary can be overwritten
Stop-Process -Name "ActionCenterExtension" -Force -ErrorAction SilentlyContinue

# 2. Build
dotnet build "ActionCenterExtension\ActionCenterExtension.csproj" -r win-x64

# 3. Register with Windows (-ForceApplicationShutdown handles any lingering process)
Add-AppxPackage -Register "ActionCenterExtension\bin\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml" -ForceApplicationShutdown
```

Then in Command Palette:

```
type "Reload" → select "Reload Command Palette extensions"
```

The extension appears in the palette immediately after reload. Dock bands appear after reload — if you added a band to the dock previously, it updates in place.

**Always run all three steps.** A build without a deploy leaves the old version running. A deploy without a reload means the palette is still talking to the old process.

## Avoiding Duplicate Dock Entries

Each time you add a band to the dock via the dock's Add menu, it creates a new pinned entry. Only add it once. If you end up with duplicates, right-click each one in the dock and select **Remove**, then add it back once.

Do not use the dock Add menu during development iterations — the band updates automatically once the extension is reloaded.

## Debug

PowerToys logs extension activation failures to `%LocalAppData%\Microsoft\PowerToys\logs`.

For verbose output, add `Debug.WriteLine(...)` calls and attach a debugger, or write to a log file from the extension process directly.

## Publish (Release Build)

```powershell
dotnet publish "ActionCenterExtension\ActionCenterExtension.csproj" -p:PublishProfile=win-x64
```

After publishing, deploy the resulting MSIX or register it with `Add-AppxPackage`.

## Common Failures

### Extension Does Not Appear In Palette

- Confirm you ran `Add-AppxPackage -Register` after building — a plain `dotnet build` does not register anything.
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

`Properties/launchSettings.json` and `*.pubxml` are committed — they are needed for deployment. The `.gitignore` in this repo already excludes them from the default exclusion list.
