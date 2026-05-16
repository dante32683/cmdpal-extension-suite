# Architecture

This is a PowerToys Command Palette extension targeting .NET 9 and `Microsoft.CommandPalette.Extensions` SDK v0.9+. It runs as an out-of-process COM server registered with PowerToys.

## How Command Palette Extensions Work

PowerToys discovers extensions through COM registration. When the palette starts:

1. It activates the COM server (launches the extension process).
2. It calls `IExtension.GetProvider(ProviderType.Commands)` to get the `ICommandProvider`.
3. It calls `TopLevelCommands()` to populate the home screen and search.
4. It calls `GetDockBands()` (if the provider implements `ICommandProvider3`) to populate dock widgets.

The extension process stays running as long as the palette is active. `Program.cs` manages the COM server lifetime — do not modify it.

## File Map

```
[ExtensionName]/
├── Program.cs                               # COM server host — do not touch
├── [ExtensionName].cs                       # IExtension impl — returns the provider
├── [ExtensionName]CommandsProvider.cs       # CommandProvider — main entry point
├── Bands/
│   └── [Feature]Command.cs                  # One file per dock band action
├── Services/
│   └── [Feature]Service.cs                  # One file per domain service (WinRT, IPC, etc.)
├── Interop/
│   └── User32.cs                            # P/Invoke declarations
├── Pages/
│   └── [Feature]Page.cs                     # ListPage or ContentPage for flyouts
└── Assets/
    └── StoreLogo.png (+ other icons)
```

## SDK Primitives

### CommandProvider

Subclass `CommandProvider` and override:

- `TopLevelCommands()` → `ICommandItem[]` — commands in the palette home screen and search.
- `GetDockBands()` → `ICommandItem[]?` — widgets in the persistent dock toolbar.

Set a non-empty `Id` on the provider — required for dock persistence:
```csharp
Id = "com.yourname.extensionname";
```

### ICommandItem / CommandItem

Pairs a command with display metadata (title, icon, subtitle). Used for both top-level commands and dock bands:
```csharp
new CommandItem(myCommand) { Title = "My Command", Icon = myCommand.Icon }
```

### InvokableCommand

A command that executes when activated. Subclass and override `Invoke()`:
```csharp
internal sealed partial class MyCommand : InvokableCommand
{
    public MyCommand()
    {
        Id = "com.yourname.extensionname.mycommand";
        Name = "My Command";
        Icon = new IconInfo(""); // Segoe Fluent Icons codepoint
    }

    public override CommandResult Invoke()
    {
        // do something
        return CommandResult.KeepOpen(); // or CommandResult.Dismiss()
    }
}
```

### ListPage / ContentPage

- `ListPage` — renders a list of `ListItem`s. When used as a dock band, each item becomes a button in the dock strip.
- `ContentPage` — freeform content with a flyout.

### Live Updates

Dock bands update their display by mutating `Title`, `Subtitle`, or `Icon` on their `ListItem`s from a timer or event. The dock reflects changes automatically.

### Icons

```csharp
// Glyph from Segoe Fluent Icons (default font):
new IconInfo("")

// File path relative to extension package:
IconHelpers.FromRelativePath("Assets\\MyIcon.png")

// Light + dark variants:
new IconInfo(new IconData(""), new IconData(""))
```

Always use explicit Unicode escapes for glyphs — do not paste glyph characters directly into source code.

## SendInput Pattern (For Key Simulation)

```csharp
// Interop/User32.cs
[LibraryImport("user32.dll")]
internal static partial uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);
// ... INPUT, KEYBDINPUT, MOUSEINPUT structs

// Usage:
User32.INPUT[] inputs =
[
    new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_LWIN } } },
    new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_A } } },
    new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_A, dwFlags = User32.KEYEVENTF_KEYUP } } },
    new() { type = User32.INPUT_KEYBOARD, union = new() { ki = new() { wVk = User32.VK_LWIN, dwFlags = User32.KEYEVENTF_KEYUP } } },
];
User32.SendInput((uint)inputs.Length, ref inputs[0], Marshal.SizeOf<User32.INPUT>());
```

The `MOUSEINPUT` struct must be included in the `INPUTUNION` so the union is the correct size (40 bytes on x64). Without it, `INPUT` is 32 bytes instead of 40 and `SendInput` rejects the `cbSize` silently.

## Dock Band Toggle Pattern

When a dock button opens a panel (window, flyout, system UI), clicking a second time re-opens it instead of closing it because:
1. The click shifts focus to the Command Palette, which closes the panel.
2. Then `Invoke()` fires and re-opens it.

Fix with a state toggle + auto-reset timer:

```csharp
private bool _isOpen;
private CancellationTokenSource? _resetCts;

public override CommandResult Invoke()
{
    if (_isOpen)
    {
        CancelReset();
        _isOpen = false;
        // focus loss already closed the panel — do nothing else
    }
    else
    {
        DoOpenAction();
        _isOpen = true;
        ScheduleReset();
    }
    return CommandResult.KeepOpen();
}

private void ScheduleReset()
{
    CancelReset();
    var cts = new CancellationTokenSource();
    _resetCts = cts;
    _ = Task.Delay(TimeSpan.FromSeconds(10), cts.Token)
          .ContinueWith(t => { if (!t.IsCanceled) _isOpen = false; });
}

private void CancelReset()
{
    _resetCts?.Cancel();
    _resetCts = null;
}
```

The 10-second timeout handles the case where the user closes the panel externally (click elsewhere, Escape, etc.) — the flag resets automatically so the next click opens correctly.
