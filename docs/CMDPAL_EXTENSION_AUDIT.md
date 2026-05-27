# Command Palette Extension Audit

Date: 2026-05-24

This audit records the locally installed Command Palette extensions and adjacent NPU development packages found while preparing the NPU Obsidian plan.

## Current Command Palette Registrations

The following packages register `com.microsoft.commandpalette` app extensions and appear in the Command Palette extension set:

| Package | Version | Source | Keep? | Notes |
|---|---:|---|---|---|
| `ActionCenterExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\ActionCenterExtension\bin\...` | Yes | Monorepo development registration. |
| `MediaControlsExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\MediaControlsExtension\bin\...` | Yes | Local adapted/fixed media controls extension. |
| `NpuAwakeExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\NpuAwakeExtension\bin\...` | Yes | Monorepo NPU extension. |
| `NpuClipboardExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\NpuClipboardExtension\bin\...` | Yes | Monorepo NPU extension. |
| `NpuDevToolboxExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\NpuDevToolboxExtension\bin\...` | Yes for now | Shell project; keep while implementing planned toolbox work. |
| `NpuImageEditorExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\NpuImageEditorExtension\bin\...` | Yes | Monorepo NPU extension. |
| `NpuNotesExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\NpuNotesExtension\bin\...` | Yes | Newly implemented notes MVP. |
| `NpuOrganizeExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\NpuOrganizeExtension\bin\...` | Yes | Monorepo NPU extension. |
| `NpuTextToolsExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\NpuTextToolsExtension\bin\...` | Yes | Monorepo NPU extension. |
| `SimpleAnalyticsExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\SimpleAnalyticsExtension\bin\...` | Yes | Monorepo development registration. |
| `TimeDateDockExtension` | 0.0.1.0 | `C:\Portable\CmdPaletteExts\src\TimeDateDockExtension\bin\...` | Yes | Monorepo development registration. |
| `Microsoft.PowerToys.SparseApp` | 0.99.1.0 | PowerToys | Yes | Host/built-in Command Palette extension package. Do not remove. |
| `ObsidianExtension` | 0.0.5.0 | third-party developer package | Temporary | Keep while building the NPU version as a reference; remove after the replacement is usable. |
| `VictorLin.EverythingCP` | 0.9.2.0 | Store | Optional | Not duplicated by this repo. Keep if you use Everything search. |
| `8LWXpg.ProcessKillerforCommandPalette` | 1.1.1.0 | Store | Optional | Not duplicated by this repo. Keep if you use process-kill commands. |
| `JiriPolasek.RecentFilesforCommandPalette` | 0.8.0.0 | Store | Optional | Not duplicated by this repo. Keep if you use recent-file commands. |
| `JiriPolasek.MediaControlsForCmdPal` | 0.12.0.0 | Store | Remove candidate | Duplicates local `MediaControlsExtension`. Prefer the local version because this repo carries dock-specific fixes and package identity cleanup. |

Latest log sample confirmed the active local extensions load successfully, including:

- `NpuNotesExtension`: 4 commands, 0 bands.
- `NpuAwakeExtension`: 8 commands, 2 bands.
- `NpuClipboardExtension`: 8 commands, 0 bands.
- `NpuTextToolsExtension`: 7 commands, 0 bands.
- `MediaControlsExtension`: 7 commands, 1 band.
- Total: 101 commands and 9 bands from 18 extensions.

## Raycast-Era Identity Packages

These packages do not register Command Palette app extensions. They look like old Raycast bridge identity registrations used to give helper binaries packaged identity for WinRT/AI APIs.

| Package | Version | Install location | Recommendation |
|---|---:|---|---|
| `NpuAwakeBridge.Identity` | 1.0.0.0 | `C:\Portable\Raycast\npu-ext-suite\npu-awake-ext\assets\bin` | Remove only if the Raycast suite is no longer used. |
| `NpuBridge.Identity` | 1.0.0.0 | `C:\Portable\Raycast\npu-ext-suite\npu-image-editor-ext\assets\bin` | Remove only if the Raycast suite is no longer used. |
| `NpuDevToolboxBridge.Identity` | 1.0.0.0 | `C:\Portable\Raycast\npu-ext-suite\npu-dev-toolbox-ext\assets\bin` | Remove only if the Raycast suite is no longer used. |
| `NpuNotesBridge.Identity` | 1.0.0.0 | `C:\Portable\Raycast\npu-ext-suite\npu-notes-ext\assets\bin` | Remove only if the Raycast suite is no longer used. |
| `NpuSandbox.Identity` | 1.0.0.0 | empty | Strong remove candidate; no install location was reported. |

## Cleanup Recommendation

Do not run the repo's `Refresh-ExtensionRegistrations.ps1 -MoveOldFoldersToRecycleBin` for this cleanup. That script is a one-time migration tool and removes/re-registers all local extensions, which resets Command Palette host settings for each package.

Recommended first pass:

1. Remove `JiriPolasek.MediaControlsForCmdPal` if local `MediaControlsExtension` is the one you use.
2. Remove `NpuSandbox.Identity` because it appears orphaned.
3. Remove the four `Npu*Bridge.Identity` Raycast packages only after deciding the old Raycast suite is no longer needed.
4. Keep `ObsidianExtension` until `NpuObsidianExtension` reaches feature parity, then remove it to avoid duplicate Obsidian search commands.

Potential removal commands, after confirmation:

```powershell
Get-AppxPackage -Name "JiriPolasek.MediaControlsForCmdPal" | Remove-AppxPackage
Get-AppxPackage -Name "NpuSandbox.Identity" | Remove-AppxPackage
Get-AppxPackage -Name "NpuAwakeBridge.Identity" | Remove-AppxPackage
Get-AppxPackage -Name "NpuBridge.Identity" | Remove-AppxPackage
Get-AppxPackage -Name "NpuDevToolboxBridge.Identity" | Remove-AppxPackage
Get-AppxPackage -Name "NpuNotesBridge.Identity" | Remove-AppxPackage
```
