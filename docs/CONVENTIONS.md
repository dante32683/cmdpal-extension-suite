# Conventions

## Code Style

- 4-space indentation.
- `PascalCase` for types and public members.
- `_camelCase` for private fields.
- `camelCase` for locals and parameters.
- Prefer SDK toolkit helpers (`IconHelpers`, `CommandItem`, `ListItem`, `WrappedDockItem`) over manual interface implementations.
- Comments explain non-obvious platform behavior only — not routine code.

## File Structure

- One dock band page or command per file under `Bands/` or `Pages/`, depending on whether the band is page-backed.
- One top-level or workflow command per file under `Commands/`.
- One service per domain under `Services/`. Services do not reference the SDK — they expose events or properties that bands consume.
- All P/Invoke declarations under `Interop/`, one file per DLL (e.g., `User32.cs`, `Dwmapi.cs`).
- `[ExtensionName]CommandsProvider.cs` wires everything together — no business logic here.
- Do not add behavior to `Program.cs` or `[ExtensionName].cs` — they are infrastructure only.

## Naming

- Band/command classes: `<Feature>Command.cs` (e.g., `QuickSettingsCommand.cs`).
- Service classes: `<Feature>Service.cs` (e.g., `BatteryService.cs`).
- Pages: `<Feature>Page.cs` (e.g., `SettingsPage.cs`).

## IDs

- Provider ID: use a stable reverse-DNS namespace per extension family, set in the `CommandProvider` constructor.
- Command ID: append the command or page name to the provider namespace, set in each command or page constructor.
- Current namespaces:
  - `com.dziad.actioncenterextension`
  - `com.dziad.timedatedockextension`
  - `com.dziad.simpleanalyticsextension`
  - `com.local.nputools.<extensionname>`
- IDs must be non-empty. Missing IDs cause silent failures in the dock.

## Icons

- Always use explicit Unicode escapes for glyphs: `new IconInfo("")`.
- Do not paste glyph characters directly into source — they can be silently corrupted.
- The default icon font is Segoe Fluent Icons. Verify codepoints against that font, not Segoe MDL2 Assets (they overlap but are not identical).
- Use `IconHelpers.FromRelativePath("Assets\\MyIcon.png")` for file-based icons.
- **Never create `new IconInfo()` inside `GetItems()`, `UpdateSearchText()`, or timer callbacks.** Declare icons as `private static readonly IconInfo` fields or in a `static readonly` lookup table. One allocation at startup, zero per render/tick. See `BatteryDockPage` for the lookup-table pattern with programmatic codepoint ranges.

## SendInput vs keybd_event

- Use `SendInput` for synthetic keypresses. `keybd_event` is deprecated.
- Include `MOUSEINPUT` in the `INPUTUNION` struct so the union is the correct size (40 bytes on x64). Omitting it causes `SendInput` to silently reject calls.
- Use `[LibraryImport]` (source-generated) over `[DllImport]` for all P/Invoke declarations.

## Error Handling

- WinRT callbacks and timer handlers must have top-level exception guards. Unhandled exceptions in these contexts crash the COM server.
- Do not swallow exceptions silently. At minimum log with `Debug.WriteLine($"Context failed: {ex.GetType().Name}: {ex.Message}")`.
- Retain WinRT source objects (battery, network, SMTC) as instance fields for process lifetime. Short-lived WinRT wrappers that are GC'd while the OS-side object is active cause `AccessViolationException` in `WinRT.IObjectReference.Finalize`.
- Unsubscribe all WinRT events and cancel timers in `Dispose`.

## Verifying A New Page Or Feature

Command Palette extensions cannot be unit-tested outside the MSIX+COM host. The verification loop is:

1. Build and deploy (`Stop-Process` → `dotnet build` → `Add-AppxPackage -Register` → "Reload Command Palette extensions"). See `RUNBOOK.md § Per-Extension Dev Loop`.
2. Open the log and confirm `Loaded N command(s)` appears with the expected count. If N is wrong, look for a crash above that line. See `RUNBOOK.md § Reading The Log`.
3. Exercise the feature in the palette. For AI-backed features, test one file before running any bulk operation.
4. If behavior is wrong and no error appears in the log, the failure is likely in `GetItems()` or `UpdateSearchText()` returning silently — add a `Debug.WriteLine` at the suspect site and redeploy.

`Debug.WriteLine` output is visible in any debugger-attached session or can be captured with tools like DebugView. For quick checks, write a temporary state file and read it with PowerShell.

## Git

- `main` — stable, deployable code. Never commit directly to main except for single-line typo fixes or doc tweaks that carry zero risk.
- Everything else goes on a branch, merged via `git merge --no-ff` to preserve history.

### Branch naming

| Prefix | Use for | Example |
|---|---|---|
| `feat/` | New commands, pages, services, or user-visible capabilities | `feat/organize-screenshot-search` |
| `fix/` | Bug fixes, crash repairs, broken behavior | `fix/rename-index-collision` |
| `chore/` | Refactors, dependency bumps, doc-only changes, tooling | `chore/update-sdk-0.9.3` |

Keep the description short and hyphenated. One branch per logical change — do not bundle unrelated work.

### Workflow

```powershell
git checkout -b feat/your-feature-name   # branch off main
# ... make changes, build, deploy, test ...
git add <specific files>
git commit -m "feat: short imperative description"
git checkout main
git merge feat/your-feature-name --no-ff -m "Merge feat/your-feature-name: one-line summary"
```

- Commits are imperative and focused (e.g., `feat: add screenshot search page`, `fix: stop OCR blocking COM thread`).
- Do not commit `bin/` or `obj/`.
- Commit `Properties/launchSettings.json` and `*.pubxml` — they are needed for deployment.

## Docs

- `ARCHITECTURE.md` — current truth for code shape. Update when the file structure or SDK usage changes.
- `RUNBOOK.md` — operational commands. Update when the build/deploy steps change.
- `CONVENTIONS.md` — standards. Update when a new pattern is established.
- `BUGS.md` — active issues. Add on discovery, resolve when fixed.
- `ROADMAP.md` — planned work. Update as features are completed or reprioritized.
- Update the smallest relevant doc when something changes — do not leave stale docs.

---

## SDK Async Rules (Critical)

### GetItems() must never block

`GetItems()` is called synchronously on the COM apartment thread. Blocking it freezes the entire Command Palette UI — no input, no rendering, no dismiss — for the duration of the block.

**Do not do this:**
```csharp
public override IListItem[] GetItems()
{
    _result = _service.DoWorkAsync().GetAwaiter().GetResult(); // BLOCKS COM THREAD
    return [...];
}
```

**Correct pattern — lazy async start on first call:**
```csharp
private int _started; // Interlocked flag

public MyResultPage(...) { IsLoading = true; }

public override IListItem[] GetItems()
{
    if (Interlocked.Exchange(ref _started, 1) == 0)
        _ = Task.Run(RunAsync);                // fires once, on first call

    if (_result == null && _errorMessage == null)
        return [/* loading placeholder */];

    // return real items
}

private async Task RunAsync()
{
    try   { _result = await _service.DoWorkAsync(); }
    catch (Exception ex) { _errorMessage = ex.Message; }
    finally { IsLoading = false; RaiseItemsChanged(); }
}
```

Use `Interlocked.Exchange` instead of starting work in the constructor. The input page creates result pages every keystroke — starting in the constructor would fire an AI call for every character the user types. The page is only navigated to when the user presses Enter, which is when `GetItems()` is first called.

### Invoke() must also not block long-running AI

`Invoke()` runs on the COM thread. For quick operations (file open, clipboard write, system call) blocking is fine. For AI calls or batch file operations, fire-and-forget with `Task.Run`:

```csharp
public override CommandResult Invoke()
{
    _ = Task.Run(DoWorkAsync);
    return CommandResult.ShowToast("Working in background…");
}
```

If you need to show a result, convert the command to a `ListPage` instead (see Result Page Pattern below).

### Result Page Pattern (for async operations that show output)

When an operation takes long and needs to display results, model it as a `ListPage`, not an `InvokableCommand`. The page becomes the command of the list item:

```csharp
// In the list page that offers the action:
new ListItem(new MyResultPage(input)) { Title = "Run Operation" }

// MyResultPage:
internal sealed partial class MyResultPage : ListPage
{
    private int _started;
    private string? _result;

    public MyResultPage(string input) { IsLoading = true; /* store input */ }

    public override IListItem[] GetItems()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
            _ = Task.Run(RunAsync);
        return _result == null ? [LoadingItem] : [ResultItem(_result)];
    }

    private async Task RunAsync()
    {
        _result = await _service.ProcessAsync(input);
        IsLoading = false;
        RaiseItemsChanged();
    }
}
```

The result page is created at list-render time (when the list page's `GetItems()` runs) but the async work only starts when the user navigates to the result page (first `GetItems()` call on that page). This is safe because the input page rebuilds its items on each keystroke.

### FallbackCommands() must return null, not empty array

```csharp
private IFallbackCommandItem[]? _fallbackCommands;  // null until loaded

private async Task InitializeFallbackCommandsAsync()
{
    _fallbackCommands = [/* ... */];
    RaiseItemsChanged();
}

public override IFallbackCommandItem[]? FallbackCommands() => _fallbackCommands;
```

Never initialize to `[]`. `null` = no fallbacks. `[]` = fallback system exists but is empty, which can confuse host-side allocation.

### RaiseItemsChanged() in ContentPage

`ContentPage.GetContent()` is only re-called when the SDK is told content changed. Subscribe to the relevant change event and call `RaiseItemsChanged()`:

```csharp
public MySettingsPage(SettingsManager mgr)
{
    _mgr = mgr;
    _mgr.Settings.SettingsChanged += OnSettingsChanged;
}

public override IContent[] GetContent() => _mgr.Settings.ToContent();

private void OnSettingsChanged(object sender, Settings args) => RaiseItemsChanged();
```

---

## UX: Dock Band

- Prefer returning **one** `CommandItem` wrapping a single `ListPage` from `GetDockBands()`. The dock renders each `ListItem` inside it as a separate strip button. A single-button band can still use a one-row page.
- Return multiple `CommandItem` bands when users need to add or remove each dock item independently from the Command Palette **Add bands** menu. Each addable band must have a clear `Title`, stable `Id`, and obvious `Icon` on the returned `CommandItem`.
- Dock buttons that open a detail view wrap a `ListPage` as their command — clicking navigates automatically. Do not use `GoToPage` / `GoToPageArgs`.
- Display-only items use `NoOpCommand`.
- `GetItems()` is **dynamic** — filter by settings and current state at call time so toggling a setting or changing state hides/shows buttons immediately without a restart.
- Mutating a `ListItem`'s `Title`, `Subtitle`, or `Icon` from a timer is reflected in the dock live.

### Refresh cadence

Only add timers when a dock item shows live state. Timers are process-lifetime and should be owned by the page, command, or service that owns the displayed state.

| Signal | Interval | Notes |
|---|---|---|
| Awake state files | On `GetItems()` | Read current state directly; no timer needed for ordinary palette pages |
| Daemon heartbeat | 5-15 s | Use only for live dock/status indicators |
| Fast local metrics | 5 s | Prefer a short initial delay when the first sample needs a baseline |
| Slow-changing local state | 30 s | Battery, power mode, or other low-frequency state |

Use `#pragma warning disable CA1001` on process-lifetime command/page classes when the SDK owns lifetime and `IDisposable` is not called reliably. Services that own external subscriptions, file watchers, or timers should implement `IDisposable` when they can be disposed deterministically.

---

## UX: Detail Pages (ListPage rows)

Each data point is a `ListItem` with `Title` (label), `Subtitle` (value), `Icon`, and an optional colored `Tag`.

### Status folding rule

Avoid standalone "Status" rows when the status describes another primary value. Fold status into the primary row's tag:

- Awake override row -> tag: `"active"` / `"sleep allowed"` / `"timed"` / `"until"`
- Schedule row -> tag: `"active"` / `"enabled"` / `"paused"`
- Daemon row -> tag: `"running"` / `"stopped"`

This keeps pages compact and puts the color signal next to the value it describes.

### Tag rules

- A `Tag` with no `Text` is invisible — always set `Text`.
- Plain or muted tags still provide useful status text.
- Keep tag text short: `"active"`, `"current"`, `"typed"`, `"preset"`, `"paused"`, `"delete"`, `"running"`.
- Prefer shared helpers such as `AwakeVisuals.StatusTag(...)`, `AwakeVisuals.MutedTag(...)`, and `AwakeVisuals.WarningTag(...)` over hand-building tags in each page.

### Page accent

Set `AccentColor` on pages when there is a clear primary state color. This tints the flyout header before the user reads any row.

### Row factory signature (standard)

Use a local row factory when a page creates repeated rows with the same shape:

```csharp
private static ListItem Row(string title, string subtitle, IconInfo icon, Tag? tag)
{
    var item = new ListItem(new NoOpCommand())
    {
        Title = title,
        Subtitle = subtitle,
        Icon = icon,
    };

    if (tag is not null)
    {
        item.Tags = [tag];
    }

    return item;
}
```

---

## UX: Settings

1. Prefer a `SettingsManager` or equivalent typed settings service that owns a single settings instance.
2. If using SDK settings, assign `Settings = _settingsManager.Settings` in the `CommandProvider` constructor so settings surface in the extension manager UI automatically.
3. If using local JSON settings, keep all reads/writes behind the domain service and normalize values on read.
4. The main command page should include a path to settings when settings are user-facing. Use a `SettingsPage` (`ContentPage`) for richer in-palette settings.
5. Pass settings services to dock/page classes via constructor injection.

### Setting types

| Type | Use for |
|---|---|
| `ToggleSetting` | Enable / disable a feature or dock item |
| `ChoiceSetSetting` | Multi-option selection, such as default mode |
| `TextSetting` | Free-form string input, such as a default local time |

Setting keys are `camelCase`: `"defaultAwakeMode"`, `"defaultDurationMinutes"`, `"defaultUntilTime"`.

---

## Color Palette

Colors should approximate Windows semantic brushes so Command Palette UI feels native.

| State | RGB | Use when |
|---|---|---|
| Green — success/active | `108, 203, 95` | Active awake state, successful action, enabled schedule |
| Yellow — caution | `255, 192, 0` | Paused schedule, warning, advanced action |
| Red — critical/destructive | `255, 95, 95` | Delete, clear, stop, or destructive action |
| Default | `default(OptionalColor)` | Normal, neutral, inactive |

```csharp
private static readonly Color GreenColor  = new Color { R = 108, G = 203, B = 95,  A = 255 };
private static readonly Color YellowColor = new Color { R = 255, G = 192, B = 0,   A = 255 };
private static readonly Color RedColor    = new Color { R = 255, G = 95,  B = 95,  A = 255 };

private static OptionalColor Colored(Color c) =>
    new OptionalColor { HasValue = true, Color = c };
```

### State color logic

```text
Manual or schedule active        -> Green tag "active"
Current default/selected option  -> Green tag "current"
Paused schedule                  -> Yellow tag "paused"
Advanced stop/delete action      -> Yellow or Red tag depending on severity
No active awake state            -> Default/muted tag "sleep allowed"
Charging / connected / healthy   -> Green
Limited / low / warning state    -> Yellow
Critical / destructive state     -> Red
```

---

## Icons (expanded)

The existing rule stands: **always use `\uXXXX` escape sequences — never paste glyphs.** When writing icon constants from a tool or script, construct the escape programmatically:

```powershell
$u = [char]0x5C + "u"   # literal backslash + u
$E713 = $u + "E713"     # safe to interpolate into file content
```

### Shared icon constants

Keep commonly reused glyphs in a visual helper class such as `AwakeVisuals` instead of scattering `new IconInfo(...)` calls across pages.

| Codepoint | Used for |
|---|---|
| `E7E8` | Power / awake toggle |
| `E916` | Clock / duration |
| `E787` | Calendar / schedule |
| `E708` | Moon / sleep allowed |
| `E945` | Sparkle / Smart Awake |
| `E8FD` | List / dashboard |
| `E71A` | Stop / cancel |
| `E713` | Settings gear |
| `E73E` | Check / enabled |
| `E701` | Wi-Fi |
| `E839` | Ethernet |
| `EEA1` | CPU chip |

### Icon lookup tables for ranged codepoints

When icons span a contiguous codepoint range (e.g., battery levels EBA0–EBAA), pre-build a `static readonly IconInfo[]` in a `static` constructor rather than constructing icons dynamically per tick:

```csharp
private static readonly IconInfo[] _batteryIcons;

static MyPage()
{
    _batteryIcons = new IconInfo[11];
    for (int i = 0; i <= 10; i++)
        _batteryIcons[i] = new IconInfo(((char)(0xEBA0 + i)).ToString());
}
```

This follows the `\uXXXX`-via-code convention (no pasted glyphs) while eliminating per-tick allocations.

---

## Services

- Instantiate services **once** in `CommandProvider` and pass by reference with constructor injection.
- Services should not reference Command Palette SDK types. They expose domain models, events, properties, and commands that pages consume.
- Methods that do not touch instance state can get `[SuppressMessage("Performance", "CA1822")]` rather than being made `static` when uniform service call sites matter.
- File-backed state should use atomic writes and normalized reads.
- Volatile metrics use rolling averages rather than instant readings:
  - Background timer fires every 5 s.
  - Ring buffer of 3 slots gives a 15 s window.
  - `private readonly object _lock = new();` guards the buffer.
  - `IsCalculating` is `true` until the first sample lands.
- AI services (`ImageDescriptionGenerator`, `LanguageModel`) must expose `async Task<T>` methods. Callers must `await` them — never `.GetAwaiter().GetResult()`. If the call site is `Invoke()` or `GetItems()`, use the async patterns documented above.

---

## NPU-Backed Content Search Pattern

This pattern enables a Command Palette page to search through files by their *content* (OCR text) and *AI description* rather than just by filename. It was first implemented in `NpuOrganizeExtension` for screenshots and is designed to be reused in other extensions for any file type.

### How it works

Three components work together:

1. **Index service** — holds a persistent, in-memory dictionary keyed by file path. Each entry stores the raw OCR text, the AI description, and the indexed timestamp. The dictionary is loaded from a JSON file at startup and written after every mutation. All public methods are guarded by a `lock` so background indexing tasks and the COM-thread search calls never race.

2. **Index page** (`IndexAllPage` pattern, a `ListPage`) — scans the target folder, skips already-indexed files, and calls both NPU models concurrently per file via `Task.WhenAll`. Shows a progress/result summary row. Uses the standard lazy async `Interlocked` start so work only begins when the user navigates to the page.

3. **Search page** (`ScreenshotSearchPage` pattern, a `DynamicListPage`) — holds the in-memory index service. `UpdateSearchText` filters the dictionary with `string.Contains` (case-insensitive) across both OCR text and AI description, then calls `RaiseItemsChanged`. Because the index is already in memory, every keystroke resolves in microseconds — no I/O, no AI calls.

### NPU models used

| Model | API | What it produces |
|---|---|---|
| `Windows.Media.Ocr.OcrEngine` | `OcrEngine.TryCreateFromUserProfileLanguages()` → `RecognizeAsync(bitmap)` | Raw text visible in the image — button labels, menu items, code, error messages |
| `Microsoft.Windows.AI.Imaging.ImageDescriptionGenerator` | `CreateAsync()` → `DescribeAsync(buffer, BriefDescription, ...)` | A natural-language sentence describing what the image shows |

Both run **concurrently** per file via `Task.WhenAll`. Either may fail silently (returns `string.Empty`) without aborting the other. Searching hits both fields, so a query like "menu" matches both OCR text that literally says "menu" and a description that says "shows a menu bar".

`OcrEngine` is always available on Windows 11 (no restricted capability needed). `ImageDescriptionGenerator` requires the `systemAIModels` restricted capability in `Package.appxmanifest` and `Microsoft.WindowsAppSDK.AI`.

### Auto-indexing on rename/write

Rename or write operations that already call the AI pipeline should also upsert the index as a side effect. Pass the index service via constructor injection to the command or page that does the write:

```csharp
var (destination, description, ocrText) = await AiNamingService.BuildProposedPathWithDataAsync(originalPath);
File.Move(originalPath, destination, overwrite: false);
_indexService.Upsert(destination, description, ocrText);
```

This keeps the index current without requiring a separate "index all" run after every operation.

### Applying to another extension

To add this pattern to a new extension:

1. Copy `Models/ScreenshotIndexEntry.cs` — rename as needed, keep the four fields (`FilePath`, `Description`, `OcrText`, `IndexedAt`).
2. Copy `Services/ScreenshotIndexService.cs` — change the `IndexPath` constant to a new path under `%LocalAppData%\NpuYourExtension\index.json`. Add fields if the domain needs them (e.g., page count for documents).
3. Create an `IndexAllPage` that enumerates the target folder and calls your AI pipeline per file, upserting to the index.
4. Create a `DynamicListPage` search page that holds the index service and filters in `UpdateSearchText`.
5. Instantiate one `YourIndexService` in `CommandProvider` and inject it wherever needed (rename commands, the search page, the index page).

The index file path, supported file extensions, and AI pipeline are the only things that change between domains. The lock/load/save/search/upsert structure is identical.

### JSON serialization (AOT)

The index service uses `System.Text.Json` source generation for AOT compatibility. The containing class must be `partial` and the nested context class must also be `partial`:

```csharp
internal sealed partial class YourIndexService
{
    [JsonSerializable(typeof(List<YourIndexEntry>))]
    [JsonSourceGenerationOptions(WriteIndented = false)]
    private sealed partial class IndexJsonContext : JsonSerializerContext { }
}
```
