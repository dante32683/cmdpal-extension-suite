# Conventions

## Code Style

- 4-space indentation.
- `PascalCase` for types and public members.
- `_camelCase` for private fields.
- `camelCase` for locals and parameters.
- Prefer SDK toolkit helpers (`IconHelpers`, `CommandItem`, `ListItem`, `WrappedDockItem`) over manual interface implementations.
- Comments explain non-obvious platform behavior only — not routine code.

## File Structure

- One band or command per file under `Bands/`.
- One service per domain under `Services/`. Services do not reference the SDK — they expose events or properties that bands consume.
- All P/Invoke declarations under `Interop/`, one file per DLL (e.g., `User32.cs`, `Dwmapi.cs`).
- `[ExtensionName]CommandsProvider.cs` wires everything together — no business logic here.
- Do not add behavior to `Program.cs` or `[ExtensionName].cs` — they are infrastructure only.

## Naming

- Band/command classes: `<Feature>Command.cs` (e.g., `QuickSettingsCommand.cs`).
- Service classes: `<Feature>Service.cs` (e.g., `BatteryService.cs`).
- Pages: `<Feature>Page.cs` (e.g., `SettingsPage.cs`).

## IDs

- Provider ID: `"com.<yourname>.<extensionname>"` — set in the `CommandProvider` constructor.
- Command ID: `"com.<yourname>.<extensionname>.<commandname>"` — set in each command constructor.
- IDs must be non-empty. Missing IDs cause silent failures in the dock.

## Icons

- Always use explicit Unicode escapes for glyphs: `new IconInfo("")`.
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

- Return **one** `CommandItem` wrapping a single `ListPage` from `GetDockBands()`. The dock renders each `ListItem` inside it as a separate strip button.
- Dock buttons that open a detail view wrap a `ListPage` as their command — clicking navigates automatically. Never use `GoToPage` / `GoToPageArgs`.
- Display-only items (e.g. CPU %) use `NoOpCommand`.
- `GetItems()` is **dynamic** — filter by settings at call time so toggling a setting hides/shows buttons immediately without a restart.
- Mutating a `ListItem`'s `Title` or `Icon` from a timer is reflected in the dock live.

### Refresh cadence

| Signal | Interval | Notes |
|---|---|---|
| Battery | 30 s | Slow-changing |
| Wi-Fi / network | 15 s | SSID / signal can change |
| CPU % | 5 s | Fast; 2 s initial delay for a valid first delta |
| Wattage samples | 5 s | 3-slot ring buffer → 15 s rolling average |

Timers are process-lifetime. Use `#pragma warning disable CA1001` on the owning class instead of implementing `IDisposable`.

---

## UX: Detail Pages (ListPage rows)

Each data point is a `ListItem` with `Title` (label), `Subtitle` (value), `Icon`, and an optional colored `Tag`.

### Status folding rule
**Never have a standalone "Status" row.** Fold status into the primary row's tag:
- Battery → `Charge` row, tag: `"Charging"` / `"Smart charging"` / `"Low"` / `"On battery"` / etc.
- Wi-Fi → `Network` row, tag: `"Connected"` (plain) / `"Limited"` (Yellow)

This keeps the list compact and puts the color signal next to the value it describes.

### Wi-Fi signal tag rules
The `Signal` row carries a colored tag rating the strength. Mirrors battery charge color logic — traffic-light scheme.

| Bars | Tag text | Color |
|---|---|---|
| 0 | `"None"` | Red |
| 1 | `"Weak"` | Red |
| 2 | `"Fair"` | Yellow |
| 3 | `"Good"` | Default |
| 4 | `"Strong"` | Green |
| 5 | `"Excellent"` | Green |

```csharp
private static (string tag, OptionalColor color) SignalTag(int bars) => bars switch
{
    0 => ("None",      Colored(RedColor)),
    1 => ("Weak",      Colored(RedColor)),
    2 => ("Fair",      Colored(YellowColor)),
    3 => ("Good",      default),
    4 => ("Strong",    Colored(GreenColor)),
    _ => ("Excellent", Colored(GreenColor)),
};
```

### Combining related rows
Merge closely related metrics into one row when they share a unit and a conceptual pair. Example: Download + Upload → single `"Speed"` row.

```csharp
// subtitle: ↓ 600 / ↑ 300 Mbps  (arrow literals via ↓ / ↑)
var speedSubtitle = $"↓ {info.ReceiveMbps:F0} / ↑ {info.TransmitMbps:F0} Mbps";
rows.Add(Row("Speed", speedSubtitle, IconDownload, default, null));
```

Only include the row when at least one value is non-zero.

### Tag rules
- A `Tag` with no `Text` is **invisible** — always set `Text`.
- Plain (uncolored) tags still provide useful status text; use `default(OptionalColor)` as the foreground.
- Keep tag text short: `"Charging"`, `"Low"`, `"Med"`, `"High"`, `"Limited"`, `"Saver"`, `"Connected"`.
- The `Row()` factory in each page class takes `(string title, string subtitle, string iconCode, OptionalColor tagColor, string? tagText)`. Attach a tag whenever `tagText is not null`.

### Page accent
Set `AccentColor` on the page to match the primary state color. This tints the flyout header before the user reads any row.

**Battery accent logic** — same as battery color logic (see Color Palette section).

**Wi-Fi accent logic** — prioritise the "worst" concern, traffic-light order:
```
Not connected              → Red
IsLimited                  → Yellow
IsWifi && SignalBars <= 1  → Red
IsWifi && SignalBars == 2  → Yellow
Otherwise (wired/good)     → Green
```

### `Row()` factory signature (standard)
```csharp
private static ListItem Row(string title, string subtitle, string iconCode,
                            OptionalColor tagColor, string? tagText)
{
    var item = new ListItem(new NoOpCommand())
        { Title = title, Subtitle = subtitle, Icon = new IconInfo(iconCode) };
    if (tagText is not null)
        item.Tags = [new Tag { Text = tagText, Foreground = tagColor }];
    return item;
}
```

---

## UX: Settings

1. `SettingsManager` owns a `Settings` instance and exposes typed property accessors.
2. Assign `Settings = _settingsManager.Settings` in the `CommandProvider` constructor — surfaces settings in the extension manager UI automatically.
3. The main command page returns a `ListItem` linking to a `SettingsPage` (`ContentPage`) so settings are also reachable from inside the palette.
4. Pass `SettingsManager` to dock/page classes via constructor injection.

### Setting types

| Type | Use for |
|---|---|
| `ToggleSetting` | Enable / disable a feature or dock item |
| `ChoiceSetSetting` | Multi-option selection (e.g. refresh interval) |
| `TextSetting` | Free-form string input |

Setting keys are `camelCase`: `"showBattery"`, `"showWifi"`, `"showCpu"`.

---

## Color Palette

Colors approximate the Windows system semantic brushes so they feel native.

| State | RGB | Use when |
|---|---|---|
| Green — success/charging | `108, 203, 95` | Charging, smart-charging, positive wattage |
| Yellow — caution/low | `255, 192, 0` | Battery ≤ 20%, energy saver, limited network, wattage 9–15 W |
| Red — critical | `255, 95, 95` | Wattage discharge > 15 W |
| Default | `default(OptionalColor)` | Normal / fully charged / light draw |

```csharp
private static readonly Color GreenColor  = new Color { R = 108, G = 203, B = 95,  A = 255 };
private static readonly Color YellowColor = new Color { R = 255, G = 192, B = 0,   A = 255 };
private static readonly Color RedColor    = new Color { R = 255, G = 95,  B = 95,  A = 255 };

private static OptionalColor Colored(Color c) =>
    new OptionalColor { HasValue = true, Color = c };
```

### Battery color logic
```
IsCharging                     → Green
IsPluggedIn && Percent < 99    → Green  (smart charging)
IsPluggedIn && Percent >= 99   → Default (fully charged)
EnergySaverOn || Percent ≤ 20  → Yellow
Otherwise                      → Default
```

### Wattage color logic
```
ChargeRateWatts > 0            → Green  tag "+"
|watts| ≤ 9 W                  → Default tag "Low"
9 W < |watts| ≤ 15 W           → Yellow tag "Med"
|watts| > 15 W                 → Red    tag "High"
```

---

## Icons (expanded)

The existing rule stands: **always use `\uXXXX` escape sequences — never paste glyphs.** When writing icon constants from a tool (Write, PowerShell here-strings), construct the escape programmatically:

```powershell
$u = [char]0x5C + "u"   # literal backslash + u
$E701 = $u + "E701"     # safe to interpolate into file content
```

### Battery icon family (Mob — preferred)
The Mob family renders better at dock button size than the standard horizontal battery icons.

| Range | Family | Use |
|---|---|---|
| `EBA0`–`EBAA` | MobBattery0–10 | Normal; one glyph per 10% |
| `EBAB`–`EBB5` | MobBatteryCharging0–10 | Charging; level-matched |
| `EBB6`–`EBC0` | MobBatterySaver0–10 | Low / energy saver; level-matched |

```csharp
var level        = Math.Clamp(info.Percent / 10, 0, 10);
var batteryIcon  = ((char)(0xEBA0 + level)).ToString();
var chargingIcon = ((char)(0xEBAB + level)).ToString();
var saverIcon    = ((char)(0xEBB6 + level)).ToString();
```

**Do not use** the standard (non-Mob) battery range `E850`–`E862` — those render as small horizontal shapes that look out of place.

### Verified codepoints

Use explicit `\uXXXX` escape sequences in source -- never paste glyphs (they corrupt silently).
Use the PowerShell construction pattern when writing files: `\uXXXX`.

| Escape | Name | Used for |
|---|---|---|
| \uE701 | WiFi | Wi-Fi full signal, Wi-Fi page icon |
| \uE871 | SignalNotConnected | No Wi-Fi / offline |
| \uE872 | Wifi1 | Weak signal |
| \uE873 | Wifi2 | Fair signal |
| \uE874 | Wifi3 | Good signal |
| \uE839 | Ethernet | Wired connection |
| \uE896 | Download | Speed row icon (use for combined download/upload row) |
| \uE898 | Upload | (avoid -- merge into Speed row with Download icon) |
| \uE917 | Clock | Time remaining |
| \uE945 | LightningBolt | Power / wattage |
| \uEA3C | Leaf | Eco / battery saver row |
| \uE713 | Settings | Settings page icon / list item |
| \uEEA1 | CPU chip | CPU usage |
| \uE73E | CheckMark | Accept / connected status |

---

## Services

- Instantiated **once** in `CommandProvider` and passed by reference (constructor injection).
- Methods that don't touch instance state get `[SuppressMessage("Performance", "CA1822")]` rather than being made `static`, so call sites stay uniform.
- Volatile metrics use **rolling averages** rather than instant readings:
  - Background timer fires every 5 s.
  - Ring buffer of 3 slots → 15 s window.
  - `private readonly object _lock = new();` guards the buffer.
  - `IsCalculating` is `true` until the first sample lands.
