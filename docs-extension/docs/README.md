# Documentation Hub

ActionCenterExtension is a PowerToys Command Palette extension that provides a persistent dock bar with system status widgets and quick-action buttons. It is a native replacement for a custom WinUI 3 menubar app that previously handled all of this from scratch.

This directory is the authoritative documentation source.

## Read Order

1. `CONTEXT.md` — why this repo exists, what it replaced, and what the migration plan is. Read this first if you are new to the project.
2. `ARCHITECTURE.md` — what the extension is, how it is shaped, and the key SDK primitives.
3. `RUNBOOK.md` — how to build, deploy, reload, and debug the extension.
4. `CONVENTIONS.md` — coding, naming, and docs standards.
5. `BUGS.md` — active issue ledger and known risks.
6. `ROADMAP.md` — planned features and milestones.
7. `CHANGELOG.md` — AI-assisted session changes.

## Authority Levels

- Canonical: `ARCHITECTURE.md`, `RUNBOOK.md`, `CONVENTIONS.md`.
- Operational: `BUGS.md`, `ROADMAP.md`, `CHANGELOG.md`.
- Orientation: `CONTEXT.md` (historical background, not current truth for code shape).

## Current Direction

Build dock bands iteratively, one widget at a time, in this priority order:

1. Quick Settings button (Win+A trigger) — the single feature that motivated the migration.
2. Clock band (live-updating).
3. Battery band (live-updating, with flyout).
4. Network band (live-updating, with flyout).
5. Volume scroll support.
6. Eye Break IPC band (if the external eye-break service is still in use).
