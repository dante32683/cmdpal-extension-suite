# Command Palette Extensions

A Windows-focused monorepo of independently packaged PowerToys Command Palette extensions. It includes dock controls, local system utilities, and NPU-backed workflows for text, images, screenshots, notes, Obsidian, and clipboard history.

Start with the [documentation hub](docs/README.md). The canonical architecture, SDK constraints, development workflow, conventions, known risks, and roadmap live under `docs/`.

## Verify the repository

The local gate is read-only with respect to package registration and user data:

```powershell
./scripts/check.ps1
```

It checks formatting, package and provider identities, Command Palette UX conventions, documentation links, transitive NuGet vulnerabilities, the full x64 build, and unit tests. Build output goes under `.artifacts/`, so installed extensions can remain running. Building into the deployable `bin/` folders and registering an extension for interactive testing is a separate workflow documented in the [runbook](docs/RUNBOOK.md#per-extension-dev-loop).

## Repository shape

- `src/` — extension projects, companion keepers, shared code, and tests
- `tools/` — developer linting and the Awake companion process
- `scripts/` — repository checks and local registration workflows
- `docs/` — authoritative engineering and operational documentation
- `references/` — ignored upstream checkouts used to verify Command Palette SDK patterns

Each packaged extension keeps a unique package identity, app-extension ID, COM class ID, and provider ID. See [Architecture](docs/ARCHITECTURE.md) before adding or copying a project.
