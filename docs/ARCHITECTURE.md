# Architecture

This repo targets .NET 9 and `Microsoft.CommandPalette.Extensions` SDK v0.9+. It contains multiple separately packageable PowerToys Command Palette extensions.

## Monorepo Layout

```text
/
├── docs/                                  # Canonical repo docs and imported extension notes
├── src/
│   ├── ActionCenterExtension/             # Quick Settings dock extension
│   ├── TimeDateDockExtension/             # Configurable time/date dock extension
│   ├── MediaControlsExtension/            # Media playback dock controls
│   ├── SimpleAnalyticsExtension/          # Battery/Wi-Fi/CPU dock analytics
│   ├── NpuAwakeExtension/                 # NPU Awake — toggle, schedules, Smart Awake, daemon
│   ├── NpuOrganizeExtension/              # NPU Organize — AI screenshot rename and search
│   ├── NpuOrganizeKeeper/                 # Companion watcher daemon for NpuOrganizeExtension
│   ├── NpuImageEditorExtension/           # NPU Image Editor — OCR, background removal, upscale
│   ├── NpuTextToolsExtension/             # NPU Text Tools — AI rewrite via Phi
│   ├── NpuClipboardExtension/             # NPU Clipboard — history, search, recorder controls
│   ├── NpuClipboardKeeper/                # Companion recorder daemon for NpuClipboardExtension
│   ├── NpuNotesExtension/                 # Markdown notes hub
│   ├── NpuDevToolboxExtension/            # NPU Dev Toolbox shell
│   ├── NpuTools.Shared/                   # Shared NPU helpers
│   ├── NpuTools.Tests/                    # xunit tests for shared services
│   └── NPUToolsExtension/                 # Original scaffold, temporary reference
├── tools/
│   └── NpuAwakeKeeper/                    # Companion daemon copied into Awake package output
├── references/
│   ├── PowerToys/                         # Ignored sparse checkout of PowerToys cmdpal sources
│   └── MediaControlsExtension/            # Ignored upstream reference clone
├── Directory.Build.props                  # Shared analyzer/platform defaults
├── Directory.Packages.props               # Central package versions
└── NpuCommandPaletteExtensions.sln        # Monorepo solution
```

## Independent Extension Packages

Each installable extension has its own:

- `.csproj`
- `Package.appxmanifest`
- `Program.cs` COM server host
- `[ExtensionName].cs` `IExtension` implementation
- `[ExtensionName]CommandsProvider.cs`
- provider ID
- MSIX package identity
- COM class GUID

Do not share those identities between projects. Do share code, docs, build props, and conventions.

## Standard Extension File Map

```text
src/[ExtensionName]/
├── Program.cs
├── [ExtensionName].cs
├── [ExtensionName]CommandsProvider.cs
├── Commands/
│   └── [Feature]Command.cs
├── Bands/
│   └── [Feature]DockPage.cs or [Feature]Command.cs
├── Pages/
│   └── [Feature]Page.cs
├── Services/
│   └── [Feature]Service.cs
├── Interop/
│   └── User32.cs
├── Assets/
│   └── StoreLogo.png
└── Package.appxmanifest
```

`Program.cs` and `[ExtensionName].cs` are infrastructure. Business logic belongs in commands, pages, services, and interop wrappers.

## How Command Palette Extensions Work

PowerToys discovers extensions through package/COM registration. When the palette starts:

1. It activates the extension process as an out-of-process COM server.
2. It calls `IExtension.GetProvider(ProviderType.Commands)`.
3. It calls `TopLevelCommands()` to populate search and the home surface.
4. It calls `GetDockBands()` when implemented to populate dock widgets.

The extension process stays running while the host needs it.

## SDK Primitives

### CommandProvider

Subclass `CommandProvider`, set `Id`, `DisplayName`, `Icon`, and optionally `Settings`.

```csharp
Id = "com.local.nputools.awake";
DisplayName = "NPU Awake";
Icon = new IconInfo("\uE7E8"); // Power glyph
```

Override:

- `TopLevelCommands()` for palette commands.
- `FallbackCommands()` for typed query workflows.
- `GetDockBands()` for dock widgets.

### CommandItem

`CommandItem` pairs a command/page with display metadata:

```csharp
new CommandItem(new SettingsPage(settingsManager))
{
    Title = "Settings",
    Subtitle = "Configure extension options",
    Icon = new IconInfo("\uE713"),
}
```

### InvokableCommand

Use `InvokableCommand` for direct actions:

```csharp
internal sealed partial class MyCommand : InvokableCommand
{
    public MyCommand()
    {
        Id = "com.local.nputools.example.mycommand";
        Name = "My Command";
        Icon = new IconInfo(""); // always use \uXXXX escapes, never paste glyphs
    }

    public override CommandResult Invoke()
    {
        return CommandResult.KeepOpen();
    }
}
```

### ListPage And ContentPage

- `ListPage` renders searchable rows and can be used as a dock-backed command surface.
- `ContentPage` hosts richer content such as SDK settings or adaptive-card forms.
- Searchable typed workflows use `SearchText`, generated typed rows, presets, and fallback commands.

### DynamicListPage (search-as-input)

Any page where the user types to filter or provide input MUST extend `DynamicListPage` and override `UpdateSearchText(string oldSearch, string newSearch)`. The SDK does NOT call `GetItems()` when the user types — it calls `UpdateSearchText()`. Failing to override it means typing does nothing and pressing Enter hits the empty-state `NoOpCommand`.

```csharp
internal sealed partial class MyInputPage : DynamicListPage
{
    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _items = BuildItems(newSearch.Trim());
        RaiseItemsChanged(_items.Length);
    }

    public override IListItem[] GetItems() => _items;
}
```

### Async Result Pages

When an operation takes time and needs to show results, model it as a `ListPage` rather than an `InvokableCommand`. The page is the command of the triggering list item. Async work starts on the first `GetItems()` call (not in the constructor — the input page recreates result pages on every keystroke, so the constructor must be cheap):

```csharp
internal sealed partial class MyResultPage : ListPage
{
    private int _started;
    private string? _result;

    public MyResultPage(string input) { _input = input; IsLoading = true; }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunAsync);
        return _result == null
            ? [new ListItem(new NoOpCommand()) { Title = "Processing…" }]
            : [new ListItem(new CopyResultCommand(_result)) { Title = _result }];
    }

    private async Task RunAsync()
    {
        try   { _result = await _service.ProcessAsync(_input); }
        catch (Exception ex) { _result = $"Error: {ex.Message}"; }
        finally { IsLoading = false; RaiseItemsChanged(); }
    }
}
```

See `RewriteResultPage`, `ImageResultPage`, and `RenameAllPage` for real examples.

## Dock Pattern

Prefer one `CommandItem` from `GetDockBands()` wrapping one `ListPage`. The dock renders each `ListItem` inside it as a button. Single-button bands can use an `InvokableCommand` when there is no detail view, but page-backed bands scale better.

Dock buttons that open system UI should use the state-toggle pattern documented in `CONVENTIONS.md` and `CONTEXT.md`.

## Shared Code

Use shared libraries when behavior is genuinely common:

- `src/NpuTools.Shared` currently serves the NPU extension family.
- Future cross-extension helpers should live in a neutral shared project only after at least two extensions need them.

Avoid moving project-specific services into shared code too early. Shared code should reduce real duplication, not hide domain behavior.

## Reference Source: PowerToys

`references/PowerToys` is a local sparse clone of `https://github.com/microsoft/PowerToys` with `src/modules/cmdpal` checked out. It is ignored by git and should be treated as read-only reference material.

Use it when implementation details are unclear, especially:

- built-in extension provider patterns under `references/PowerToys/src/modules/cmdpal/ext`
- syntax for pages, fallback commands, icons, settings, and command results
- package/project patterns used by Microsoft-maintained Command Palette extensions

Good starting points:

- `references/PowerToys/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.TimeDate`
- `references/PowerToys/src/modules/cmdpal/ext/SamplePagesExtension`
- `references/PowerToys/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.System`

Do not copy large blocks blindly. Prefer matching SDK usage and adapting structure to this repo's conventions.

`references/MediaControlsExtension` is an ignored clone of `https://github.com/jiripolasek/MediaControlsExtension`. The local `src/MediaControlsExtension` package is the adapted copy with separate package/provider identities and dock-specific fixes.
