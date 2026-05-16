# Claude Orientation

This repo uses the same documentation hub as `AGENTS.md`.

**Start here — read in this order:**

1. `docs/README.md`
2. `docs/CONTEXT.md` — read this before anything else; it explains why this repo exists and where it came from
3. `docs/ARCHITECTURE.md`
4. `docs/RUNBOOK.md`
5. `docs/CONVENTIONS.md`
6. `docs/BUGS.md`
7. `docs/ROADMAP.md`

Do not treat archived notes as current truth unless a canonical doc points to them.

## Gemini Helper — prefer this over spawning Haiku agents

`tools/gemini-subagent.ps1` wraps Gemini CLI for lightweight advisory work. **Use this instead of spawning a Claude/Haiku subagent** whenever the task is read-only or advisory — it costs far fewer tokens.

When to reach for it:
- Reviewing a git diff for bugs or risks
- Getting a second opinion on a plan or design
- Scoped exploration ("summarize this module")
- Any task that doesn't need file writes or tool use

When NOT to use it (use a Claude subagent instead):
- Direct file editing across multiple files
- Running tests or builds as part of the task
- Managed parallelism with other agents

Default invocation:

```powershell
.\tools\gemini-subagent.ps1 -Mode Review -Prompt "Review the current git diff for likely bugs. Return only concrete findings."
.\tools\gemini-subagent.ps1 -Mode Explore -Prompt "Summarize the architecture of this repo."
```

Treat output as advisory only. Verify claims locally before changing code.
