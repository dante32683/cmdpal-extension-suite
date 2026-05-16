# Roadmap

## Current Migration

Status: in progress

1. Consolidate Action Center, Simple Analytics, and NPU tools into one monorepo. Status: done.
2. Preserve available git history from old repos. Status: done for Action Center via `imported/action-center`; Simple Analytics had no `.git` repo.
3. Keep every extension separately packageable. Status: done; each project keeps its own package manifest.
4. Apply shared conventions across all imported extensions. Status: done for initial pass.
5. Verify solution build. Status: done for `dotnet build NpuCommandPaletteExtensions.sln -p:Platform=x64`.

## Implemented Extensions

- Action Center: Quick Settings dock button with settings-controlled toggle reset.
- Simple Analytics: battery, Wi-Fi/network, CPU dock/status views.
- NPU Awake: toggle, timed duration, until local time, schedules, dashboard, Smart Awake, fallback commands, and daemon integration.

## Shell Projects

- NPU Organize
- NPU Image Editor
- NPU Text Tools
- NPU Notes
- NPU Dev Toolbox

## Next Work

- Decide whether to remove old sibling folders from `C:\Portable` after separate explicit approval.
- Decide whether to retire `src/NPUToolsExtension` after replacement packages cover its purpose.
- Add CI for restore/build.
- Publish release artifacts per extension.
- Continue Raycast migration using `RAYCAST_MIGRATION_PLAN.md`.
