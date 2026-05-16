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
  - `com.dziad.simpleanalyticsextension`
  - `com.local.nputools.<extensionname>`
- IDs must be non-empty. Missing IDs cause silent failures in the dock.

## Icons

- Always use explicit Unicode escapes for glyphs: `new IconInfo("\uE713")`.
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

---

## UX: Dock Band

- Prefer returning **one** `CommandItem` wrapping a single `ListPage` from `GetDockBands()`. The dock renders each `ListItem` inside it as a separate strip button. A single-button band can still use a one-row page.
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
| `E837` | Ethernet |
| `EB9F` | CPU / processor |

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
