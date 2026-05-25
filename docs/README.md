# Documentation Hub

This repository is the monorepo for local PowerToys Command Palette extensions. Each extension remains its own MSIX package with its own provider ID, COM class GUID, package identity, and install/uninstall lifecycle.

The repo-level docs are the authoritative standards for all extensions in this workspace.

## Read Order

1. `CONTEXT.md` - what this monorepo is and why the extensions live together.
2. `ARCHITECTURE.md` - layout, SDK primitives, project structure, and shared patterns.
3. `RUNBOOK.md` - build, deploy, reload, publish, and git workflow.
4. `CONVENTIONS.md` - coding, naming, UX, settings, icons, services, and docs standards.
5. `BUGS.md` - active issue ledger and known risks.
6. `ROADMAP.md` - migration status and planned work.
7. `PROGRESS.md` - feature-level completion status and what to build next.

## Current Extension Projects

- `src/ActionCenterExtension` - Quick Settings / Action Center dock extension.
- `src/TimeDateDockExtension` - configurable time/date dock buttons that open Notification Center.
- `src/MediaControlsExtension` - media playback dock controls with compact dock fixes.
- `src/SimpleAnalyticsExtension` - battery, Wi-Fi, CPU, and lightweight system analytics dock extension.
- `src/NpuAwakeExtension` - NPU Awake: toggle, schedules, Smart Awake, daemon.
- `src/NpuOrganizeExtension` - NPU Organize: AI screenshot rename, content-indexed search, watcher daemon.
- `src/NpuImageEditorExtension` - NPU Image Editor: OCR, background removal, upscale.
- `src/NpuTextToolsExtension` - NPU Text Tools: six AI rewrite modes via Phi.
- `src/NpuClipboardExtension` - NPU Clipboard: history, search, recorder controls.
- `src/NpuNotesExtension` - Markdown notes hub with create, search, browse, pin, and delete flows.
- `src/NpuDevToolboxExtension` - NPU developer toolbox shell.
- `src/NPUToolsExtension` - original scaffold retained only as a temporary reference.
- `src/NpuTools.Shared` - shared NPU helpers.
- `tools/NpuAwakeKeeper` - companion daemon packaged by `NpuAwakeExtension`.

## Extension-Specific Notes

Imported project docs live under `docs/extensions/`. Treat them as historical or extension-specific notes unless a root canonical doc points to them.

- `docs/extensions/action-center`
- `docs/extensions/simple-analytics`

## Local Reference Checkouts

- `references/PowerToys` - ignored sparse clone of `microsoft/PowerToys`, checked out to `src/modules/cmdpal`. Use it as the first source for fully correct Command Palette extension syntax and SDK usage examples.
- `references/MediaControlsExtension` - ignored clone of `jiripolasek/MediaControlsExtension`, used as the source reference for the local media-controls port.

## Planning Documents

- `RAYCAST_MIGRATION_PLAN.md` - migration plan for Raycast tools into Command Palette extensions. This is planning material, not current architecture truth until implemented.
- `NPU_NOTES_EXTENSION_GUIDE.md` - implementation guide for the Raycast-inspired NPU Notes extension.
- `docs/ephemeral-plans/` - ignored local planning notes for future agents on this machine. These files are intentionally excluded from git and are not authoritative unless copied into canonical docs.

## Authority Levels

- Canonical: `ARCHITECTURE.md`, `RUNBOOK.md`, `CONVENTIONS.md`
- Operational: `BUGS.md`, `ROADMAP.md`
- Historical/reference: `docs/extensions/*`, copied PDFs, old extension docs, `references/PowerToys`
