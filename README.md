# SMS — School Management System (build repository)

Fresh build repository per Implementation Plan v1.0 (`../docs/Implementation/`), started 2026-08-14 (stage S0, epic E-001).

- **Requirements authority:** Analysis v1.0 in `../docs/` — changes only via change requests.
- **Runtime:** .NET 5 per CR-2 (owner directive 2026-08-14; EOL risk accepted and recorded in ADR-6 history and the risk register).
- **Architecture:** Clean Architecture modular monolith (ADR-1): `Sms.Domain` ← `Sms.Application` ← `Sms.Infrastructure` / `Sms.Web`. Dependency directions are CI-enforced by `tests/Sms.ArchitectureTests`.
- **BR coverage (NF-M5):** tests verifying a numbered business rule carry `[BusinessRule("BR-XXX-###")]` from `Sms.TestSupport`; CI diffs traits against the rule ids in `../docs/`.

Build: `dotnet build` · Test: `dotnet test` (SDK pinned by `global.json`).
