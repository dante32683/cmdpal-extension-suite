# Roadmap

## Current Migration

Status: in progress

1. Consolidate Action Center, Simple Analytics, and NPU tools into one monorepo. Status: in progress.
2. Preserve available git history from old repos. Status: in progress for Action Center; Simple Analytics had no `.git` repo.
3. Keep every extension separately packageable. Status: in progress; each project keeps its own package manifest.
4. Apply shared conventions across all imported extensions. Status: in progress.
5. Verify solution build and selected per-extension builds. Status: pending.

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

- Finish convention pass across imported extensions.
- Decide whether to retire `src/NPUToolsExtension` after replacement packages cover its purpose.
- Add CI for restore/build.
- Publish release artifacts per extension.
- Continue Raycast migration using `RAYCAST_MIGRATION_PLAN.md`.
