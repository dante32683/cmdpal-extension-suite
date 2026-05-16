# Runbook

## Requirements

- Windows 11 (recommended).
- .NET 9 SDK or newer.
- PowerToys v0.98.0 or newer (Command Palette dock support).
- `Microsoft.CommandPalette.Extensions` SDK ≥ 0.9.260303001 (check `Directory.Packages.props`).

## Dev Loop (Build → Deploy → Reload)

Visual Studio is not required. Run these three commands after every change:

```powershell
# 1. Kill the running extension process so the binary can be overwritten
Stop-Process -Name "[ExtensionName]" -Force -ErrorAction SilentlyContinue

# 2. Build
dotnet build "[ExtensionName]\[ExtensionName].csproj" -r win-x64

# 3. Register with Windows (-ForceApplicationShutdown handles any lingering process)
Add-AppxPackage -Register "[ExtensionName]\bin\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml" -ForceApplicationShutdown
```

Replace `[ExtensionName]` with your actual project folder and binary name.

Then in Command Palette:

```
type "Reload" → select "Reload Command Palette extensions"
```

**Always run all three steps.** A build without a deploy leaves the old version running. A deploy without a reload means the palette is still talking to the old process.

## Avoiding Duplicate Dock Entries

Each time you add a band to the dock via the dock's Add menu, it creates a new pinned entry. Only add it once. If you end up with duplicates:
1. Right-click each one in the dock → **Remove**.
2. Reload the extension.
3. The band updates automatically — do not re-add it via the Add menu.

Do not use the dock Add menu during development iterations. The band updates automatically after reload.

If the log shows `Skipping duplicate pinned dock band command`, the dock has two pinned registrations for the same ID. Follow the remove + reload steps above.

## Debug

PowerToys logs extension activation failures to:
```
%LocalAppData%\Microsoft\PowerToys\CmdPal\Logs\
```

Check the most recent log file for errors. Common entries:
- `Failed to load commands from extension` — COM activation failed; check the GUID in `Package.appxmanifest` matches `[ExtensionName].cs`.
- `Skipping duplicate pinned dock band command` — duplicate dock registration; remove and re-add (see above).
- `Failed to find band [id]` — band ID from a previous extension version is no longer provided; benign unless the band should still exist.

For verbose output, add `Debug.WriteLine(...)` calls and attach a debugger (F5 in Visual Studio with `-RegisterProcessAsComServer` launch args), or write to a log file from the extension process.

## Publish (Release Build)

```powershell
dotnet publish "[ExtensionName]\[ExtensionName].csproj" -p:PublishProfile=win-x64
```

After publishing, deploy the resulting MSIX or register with `Add-AppxPackage`.

## Common Failures

### Extension Does Not Appear In Palette

- Confirm you ran `Add-AppxPackage -Register` after building — `dotnet build` alone does not register anything.
- Run the Reload command in Command Palette.
- Check the CmdPal logs for activation errors.
- Confirm the GUID in `[ExtensionName].cs` matches what is in `Package.appxmanifest`.

### Dock Band Does Not Appear

- Confirm `GetDockBands()` is overridden on the `CommandProvider`.
- Confirm every returned `ICommandItem` has a `Command` with a non-empty `Id`.
- Confirm the `CommandProvider` itself has a non-empty `Id` set.
- Run the Reload command after deploy.
- PowerToys dock must be enabled in PowerToys settings.

### Build Errors About Missing SDK Types

- Confirm `Microsoft.CommandPalette.Extensions` version is ≥ 0.9.260303001 in `Directory.Packages.props`.
- `GetDockBands` and `WrappedDockItem` are only available in SDK 0.9+.

### COM Activation Fails

- Do not modify the COM server registration pattern in `Program.cs`.
- The process must be launched with `-RegisterProcessAsComServer` by the PowerToys host — do not launch it manually in production (F5 debug launch is fine).

### Dock Button Opens Panel Instead Of Toggling It

See CONTEXT.md "Dock Band Toggle Behavior" and ARCHITECTURE.md "Dock Band Toggle Pattern". The fix is a state-tracking boolean with an auto-reset timer, not window detection. Win32 window detection is unreliable for system UI panels on Windows 11.

### Icon Not Showing On Dock Band

1. Verify the glyph codepoint is correct: in PowerShell, `[int][char]'<paste-glyph>'` should return the expected hex value.
2. Use an explicit Unicode escape in source (`""`) rather than pasting the character directly.
3. If the icon still does not appear after a clean deploy, the dock may be showing a stale cached entry. Right-click the dock band → Remove, reload, and let it re-register.
