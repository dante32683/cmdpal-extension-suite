# ActionCenterExtension

A PowerToys Command Palette extension that adds a Quick Settings button to the dock — a single click to toggle the Windows Quick Settings panel (Win+A).

This is a native replacement for a custom WinUI 3 menubar app. Clock, battery, and network are built into the PowerToys dock natively and are not duplicated here.

## Requirements

- Windows 10 1809 or newer (Windows 11 recommended)
- .NET 8 SDK
- PowerToys v0.98.0 or newer
- Visual Studio 2022 with the Windows App SDK workload

## Build and Deploy

```powershell
dotnet build ActionCenterExtension\ActionCenterExtension.csproj
```

To register the extension with PowerToys, use **Build > Deploy Solution** in Visual Studio. A plain `dotnet build` does not register the extension.

After deploying, reload in Command Palette:

```
Command Palette → type "Reload" → select "Reload Command Palette extensions"
```

## Documentation

Full docs are in [`docs-extension/docs/`](docs-extension/docs/README.md). Start with [`CONTEXT.md`](docs-extension/docs/CONTEXT.md) if you are new to the project.
