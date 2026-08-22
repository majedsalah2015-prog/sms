# SMS — School Management System (build repository)

Fresh build repository per Implementation Plan v1.0 (`../docs/Implementation/`), started 2026-08-14 (stage S0, epic E-001).

- **Requirements authority:** Analysis v1.0 in `../docs/` — changes only via change requests.
- **Runtime:** .NET 5 per CR-2 (owner directive 2026-08-14; EOL risk accepted and recorded in ADR-6 history and the risk register).
- **Architecture:** Clean Architecture modular monolith (ADR-1): `Sms.Domain` ← `Sms.Application` ← `Sms.Infrastructure` / `Sms.Web`. Dependency directions are CI-enforced by `tests/Sms.ArchitectureTests`.
- **BR coverage (NF-M5):** tests verifying a numbered business rule carry `[BusinessRule("BR-XXX-###")]` from `Sms.TestSupport`; CI diffs traits against the rule ids in `../docs/`.

Build: `dotnet build` · Test: `dotnet test` (SDK pinned by `global.json`).

## Working with an AI agent on this repository

The conventions this codebase enforces are written down once, in files the agent loads
automatically — so a task started in a fresh session follows the same pattern as the last one.

| File | Holds |
|---|---|
| `CLAUDE.md` | the standing law: layout, layering, audit tiers, permissions, bilingual rules, the EF traps each of which was paid for once |
| `.claude/skills/sms-task/` | **the working pattern every task follows** — understand → scope → build → verify → land, plus the definition of done |
| `.claude/skills/sms-engine/` · `sms-screen/` · `sms-seed/` | how a slice, a screen, and seeded reference data are built here |
| `.claude/skills/sms-erp/` | the embedded-ERP boundary and how a change reaches the ERP repository |
| `.claude/skills/sms-smoke/` · `sms-review/` | running the product for a real check, and reviewing a diff against this codebase's own failure classes |
| `.claude/agents/` | four scoped roles — spec reader, implementer, verifier, reviewer |
| `.claude/settings.json` | the shared permission rules (`external/erp` is not editable here; no blanket `git add`) |

Start a session from this directory so all of it loads.
