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
- Time Date Dock: separate configurable time and date dock buttons that open Notification Center.
- Media Controls Dock: media playback controls with compact dock subtitle suppression and native glyph controls.
- Simple Analytics: battery, Wi-Fi/network, CPU dock/status views.
- NPU Awake: toggle, timed duration, until local time, schedules, dashboard, Smart Awake, fallback commands, and daemon integration.

## Implemented Extensions (continued)

- NPU Organize: screenshot rename proposals with date-slug, dry-run mode, watcher dashboard (OrganizeKeeper stub), hub page.
- NPU Image Editor: OCR via OcrEngine, background removal via ImageObjectExtractor, 2x super-resolution via ImageScaler, hub + per-operation input pages.
- NPU Text Tools: six rewrite modes (Fix Grammar, Make Formal, Make Concise, Bullet Points, Simplify, Custom) via Phi LanguageModel, hub + per-mode input pages.

## Shell Projects

- NPU Notes
- NPU Dev Toolbox

## Next Work

- Add CI for restore/build.
- Publish release artifacts per extension.
- Implement NPU Notes (add/browse/delete/find related/search per migration plan Phase 5).
- Implement NPU Dev Toolbox (open workspace in Explorer/terminal/IDE per migration plan Phase 6).
- Build OrganizeKeeper daemon companion exe for the watcher feature.
- Continue Raycast migration using `RAYCAST_MIGRATION_PLAN.md`.
