# Architecture

ActionCenterExtension is a PowerToys Command Palette extension targeting .NET 8 and the `Microsoft.CommandPalette.Extensions` SDK v0.9+. It runs as an out-of-process COM server registered with PowerToys.

## How Command Palette Extensions Work

PowerToys Command Palette discovers extensions through COM registration. When the palette starts:

1. It activates the COM server (launching the extension process).
2. It calls `IExtension.GetProvider(ProviderType.Commands)` to get the `ICommandProvider`.
3. It calls `TopLevelCommands()` to populate the home screen.
4. It calls `GetDockBands()` (if the provider implements `ICommandProvider3`) to populate dock widgets.

The extension process stays running as long as the palette is active. `Program.cs` manages the COM server lifetime — do not modify it.

## Current File Map

```
ActionCenterExtension/
├── Program.cs                              # COM server host — do not touch
├── ActionCenterExtension.cs                # IExtension impl — returns the provider
├── ActionCenterExtensionCommandsProvider.cs # CommandProvider — main entry point
├── Pages/
│   └── ActionCenterExtensionPage.cs        # Stub ListPage — replace with real pages
├── Assets/
│   └── StoreLogo.png (+ other icons)       # Extension icons
└── Properties/
    ├── launchSettings.json                 # Debug launch config
    └── PublishProfiles/
        ├── win-x64.pubxml
        └── win-arm64.pubxml
```

## Target Shape

As features are added, the structure should grow like this:

```
ActionCenterExtension/
├── Program.cs
├── ActionCenterExtension.cs
├── ActionCenterExtensionCommandsProvider.cs  # Wires all bands + commands
├── Bands/
│   ├── QuickSettingsBand.cs                  # Win+A button
│   ├── ClockBand.cs                          # Live clock
│   ├── BatteryBand.cs                        # Battery status + flyout
│   ├── NetworkBand.cs                        # Network status + flyout
│   └── EyeBreakBand.cs                       # Eye break IPC (if needed)
├── Services/
│   ├── BatteryService.cs                     # WinRT battery API
│   ├── NetworkService.cs                     # WinRT network API
│   └── EyeBreakIpcService.cs                 # Named pipe client
├── Interop/
│   └── User32.cs                             # SendInput + any other P/Invokes
├── Pages/
│   └── (pages for flyout content)
└── Assets/
```

## SDK Primitives

### CommandProvider

The main class. Subclass `CommandProvider` and override:

- `TopLevelCommands()` → `ICommandItem[]` — commands shown in the palette home screen and search.
- `GetDockBands()` → `ICommandItem[]?` — widgets shown in the persistent dock toolbar. Requires `ICommandProvider3`.
- `GetCommandItem(string id)` → allows nested commands to be pinned to the dock. Requires `ICommandProvider4`.

Set `Id` on the provider (e.g., `"com.dziad.actioncenterextension"`) — required for dock persistence.

### ICommandItem / CommandItem

A wrapper that pairs a command with display metadata (title, icon, subtitle). Used in both `TopLevelCommands` and `GetDockBands`.

### IInvokableCommand

A command that does something when executed. Use for the Quick Settings button, any toggle, any action.

### ListPage / IListPage

A page that renders a list of items. When used as a dock band command, each list item becomes a separate button in the dock strip.

### IContentPage

A page with freeform content. When used as a dock band command, renders as a single button with a flyout.

### WrappedDockItem

A helper that wraps multiple `ListItem`s into a single dock band strip. Use when you want multiple buttons side by side (e.g., battery + network + clock in one strip).

### Live Updates

Dock bands can update their content by mutating `Title`, `Subtitle`, or `Icon` on a `ListItem` from a timer or event. The dock reflects changes automatically.

## Event Sources (What We Will Use)

Unlike the old app which managed all event sources manually, most infrastructure events (fullscreen, docking, DPI) are handled by PowerToys. What the extension manages:

- **WinRT events**: battery report updates, network status changes (for battery/network bands).
- **Timers**: clock tick (1-minute or 1-second depending on whether seconds are shown), battery/network polling fallback if WinRT events are insufficient.
- **P/Invoke**: `SendInput` for Win+A (Quick Settings), `GetCursorPos` if volume scroll is implemented.
- **Named pipe**: Eye Break IPC if that feature is ported.

## Critical Invariants

- `Program.cs` COM server pattern must not be modified.
- Deploy via MSIX, not loose files — the extension is registered as a packaged app.
- All dock band `ICommandItem` objects must have non-empty `Command.Id` values.
- WinRT event subscriptions must be unsubscribed on disposal to avoid the same finalizer crash class that affected the old app.
- UI-thread-sensitive SDK calls should use the dispatcher if called from background threads (verify per SDK docs as the model differs from WinUI 3).
