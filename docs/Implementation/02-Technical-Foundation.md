# IP 02 — Technical Foundation Plan

**Phase:** IP-2 | **Status:** ✅ **Gate IP-2 approved 2026-08-14**; O6 spike **authorized 2026-08-14** (result to be recorded as §4 addendum) | **Inputs:** [../02-System-Architecture.md](../02-System-Architecture.md) (ADR-1..7, T-1..T-8), [01-Entry-Decisions.md](01-Entry-Decisions.md) (O6, O7, O10)

> Plan, not code. This fixes the solution skeleton, toolchain, and the three technical decisions deferred from IP-1. Build of any of it starts only after Gate IP-7.

---

## 1. Solution skeleton (realizes ADR-1 Clean Architecture)

One solution, module boundaries enforced by project structure + architecture tests:

```
src/
  Sms.Domain/            # entities, value objects (Money, PersonName, HijriDate), domain events — no dependencies
  Sms.Application/       # use cases (commands/queries), validators, authorization policies, ports
  Sms.Infrastructure/    # EF Core, identity, file storage, gateways, PDF engine, jobs, caching
  Sms.Web/               # MVC: one area per module + Portal area; localization middleware; UI per docs/UI
tests/
  Sms.Domain.Tests/  Sms.Application.Tests/  Sms.Infrastructure.Tests/  Sms.Web.Tests/
  Sms.ArchitectureTests/ # dependency-direction + module-boundary rules (NetArchTest) — CI-enforced
  Sms.E2E/               # Playwright: AR + EN journeys, screenshot gates
tools/
  Sms.Seeder/            # demo tenant bilingual seed data (doc 02 §9 — also the QA fixture)
```

- Module partitioning **inside** the four projects by namespace/folder (`Modules/Fees/...`), not 36 separate projects — boundaries enforced by architecture tests, keeping build times sane.
- `SchoolId` + academic-year scoping (ADR-2/3) implemented once: EF global query filters + ambient tenant/year context; architecture test asserts no module opts out.
- **Note:** `E:\school2028` is not currently a git repository; the existing `src/`, `tests/`, `tools/`, `ERP2028.sln` predate this engagement. Build starts from a **fresh repository** initialized at IP-7 approval; nothing from the current workspace is reused without review.

## 2. Toolchain & library plan (v1)

| Concern | Selection | Note |
|---------|-----------|------|
| Runtime / web | **.NET 5** (CR-2 owner directive 2026-08-14; EOL risk accepted — see ADR-6 history), ASP.NET Core 5 MVC | T-4: Bootstrap RTL + thin jQuery |
| Data | EF Core 5 + SQL Server 2019+ | Migrations = schema source of truth (T-2); naming per docs/Database/01 |
| Use-case layer | MediatR + FluentValidation | Command/query handlers map 1:1 to BR-numbered validations |
| Background jobs | Hangfire (SQL storage) | T-6 in-process acceptable v1; queued reports, rollover, notifications, backup verification |
| Logging | Serilog → file + SQL sink | Correlates with audit T0–T3 (audit is its own subsystem, not logging) |
| Testing | xUnit, FluentAssertions, Respawn, Playwright | **NF-M5 gate: every numbered BR maps to ≥1 automated test** — enforced by a BR-coverage report in CI |
| Identity | ASP.NET Core Identity, cookie auth | Per doc 06; portal + staff separated by area policies |
| PDF/reporting | **Spike decision — §4** | |

## 3. Environments, CI/CD, branching

- **Environments:** Dev → QA → Staging (demo tenant, bilingual seed) → Production per customer (doc 02 §9). EF migrations run per tenant on upgrade with pre-op snapshot (per M35 rules).
- **CI (every PR):** build + analyzers → unit/integration tests → architecture tests → BR-coverage report → AR/EN screenshot diff on flagged screens (the RTL quality gate from the risk register).
- **Branching:** trunk-based; short-lived feature branches; PR review mandatory; commits/tests reference BR ids for traceability.
- **Coding standards:** `.editorconfig` + Roslyn analyzers (warnings as errors); nullable enabled; resource-file (.resx) AR/EN conventions from docs/UI; Hijri conversions only via the Umm al-Qura domain service (ADR-4); `TimeProvider` abstraction — no direct `DateTime.Now`.

## 4. O6 — PDF/reporting engine: shortlist + spike (approval requested at this gate)

Criteria fixed at sign-off: Arabic shaping/RTL fidelity, embedded Arabic fonts, tagged (accessible) PDF, plus licensing cost for a commercial product.

| Candidate | Why shortlisted | Watch-out |
|-----------|-----------------|-----------|
| QuestPDF | Modern .NET-native, good RTL support, predictable layout code | Commercial license tier required at product revenue; tagged-PDF support to verify |
| Syncfusion PDF/Reporting | Mature Arabic support, tagged PDF, report designer | Per-developer licensing cost |
| DevExpress Reporting | Strong designer + Arabic, XtraReports ecosystem | Cost; heavier dependency |

**Spike plan (time-boxed 3 days, throwaway code, per ground rule 2):** each candidate renders two acceptance documents from fixed fixtures — (a) an Arabic report card (RTL tables, mixed AR/EN, Hijri+Gregorian dates, Arabic-Indic digits) and (b) a ZATCA simplified tax invoice (QR, 15% VAT lines). Score: fidelity (print + screen), tagged-PDF output, code ergonomics, license cost over 3 years. Decision recorded here as an addendum.

> **Addendum (E-803, 2026-08-18) — QuestPDF spike executed: FAILS RTL fidelity on .NET 5.** Both acceptance documents rendered with QuestPDF 2022.12 / 2023.12 (the last net5-compatible releases): Arabic shaping and page mirroring are correct, but there is **no bidi run reordering** — embedded Latin words and *all* digit sequences (Gregorian years, marks, invoice amounts, VAT number) print reversed; tagged PDF unsupported. Correct bidi lands only in QuestPDF 2024.3+ (net6+). **The O6 decision is therefore coupled to CR-2 (.NET 5)** — every shortlisted engine's bidi-correct release is net6+. Syncfusion/DevExpress not exercised (no licence in the build environment). Evidence, fixtures and options: [Spikes/O6-QuestPDF/README.md](Spikes/O6-QuestPDF/README.md). **O6 remains open — needs a tech-lead/owner decision (re-open CR-2, or licence a netstandard2.0 commercial engine and re-run the spike).**

## 5. O7 — Hosting: decision posture

- **Planning posture (locked at IP-1):** cloud with **in-KSA data residency** (PDPL) as default; on-prem remains supported via the existing file-storage/backup abstractions (T-7).
- **Evaluation (with commercial, before IP-6):** candidates limited to providers with KSA regions and SQL Server support (e.g., Azure KSA/partner regions, Oracle Cloud KSA, STC Cloud); criteria: residency, managed-SQL availability, backup/DR features matching M35 requirements, cost per school.
- For estimation purposes IP-4 assumes **IaaS/managed SQL in-KSA**; hosting choice does not change application architecture (abstractions already in place) — it only prices operations.

## 6. O10 — Salary-field encryption: **DECIDED — SQL Server Always Encrypted (randomized)**

Rationale: keeps salary plaintext out of the DB engine, memory dumps, and backups; satisfies the sensitivity tiering in doc 06/07 without app-managed key code. Consequence accepted: no server-side computation on encrypted columns — payroll-preparation aggregates compute app-side, which is fine at school scale (hundreds of employees, monthly batch). Key management: column master key in the platform key vault (cloud) or Windows cert store (on-prem). If pilot-scale testing ever falsifies the scale assumption, fallback is enclave-enabled Always Encrypted — schema unchanged.

## 7. Gate IP-2 asks

1. Approve this technical foundation plan (skeleton, toolchain, environments, CI/CD, standards, O7 posture, O10 decision).
2. **Separately authorize the O6 PDF-engine spike** (3-day time-box, throwaway, decision recorded as addendum here) — required by ground rule 2 before any spike code is written.
