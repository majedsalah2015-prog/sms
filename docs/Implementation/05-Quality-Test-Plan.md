# IP 05 — Quality & Test Plan

**Phase:** IP-5 | **Status:** ✅ **Gate IP-5 approved 2026-08-14** | **Inputs:** NF-M5 (BR→test mapping), risk register, [02-Technical-Foundation.md](02-Technical-Foundation.md) §2–3, docs/UI (a11y/RTL program), docs/Database/04 (perf gates)

---

## 1. Test pyramid & ownership

| Level | Scope | Tooling | Written by |
|-------|-------|---------|-----------|
| Domain unit | Entities, value objects (Money/VAT, HijriDate), BR enforcement | xUnit | Feature developer |
| Application | Command/query handlers, validators, authorization policies — **primary home of BR tests** | xUnit + in-memory ports | Feature developer |
| Integration | EF mappings, query filters (SchoolId/year), usp_IssueNumber concurrency, Always Encrypted round-trips | xUnit + real SQL Server (containerized), Respawn | Feature developer |
| Architecture | Layer dependencies, module boundaries, no-DateTime.Now, no filter opt-out | NetArchTest suite | Tech lead |
| E2E | Persona journeys in AR **and** EN (register student → invoice → pay → receipt; mark entry → report card) | Playwright | QA automation |
| Visual/RTL | Screenshot diff on flagged screens, both languages, LTR/RTL | Playwright snapshots | QA automation + bilingual QA |
| Manual | Exploratory per epic; bilingual content review; pilot scenarios | Session-based | Bilingual QA/BA |

## 2. The BR-coverage gate (NF-M5) — mechanism

1. Every test that verifies a numbered rule carries a trait: `[BusinessRule("BR-FEE-012")]`.
2. CI job extracts all BR ids from `docs/` (regex over the baselined analysis) and diffs against traits in the test suite.
3. Output: **BR coverage report** — per module: covered / uncovered / not-yet-built (epic not started, per the S0–S8 map).
4. **Gate:** an epic cannot close with uncovered BRs in its modules; release cannot ship with regressions in previously covered BRs.
5. Rules that are configuration-dependent (the ~25 O8 pilot defaults) are tested at both default and at least one non-default setting.

## 3. Financial integrity — three layers under test (risk register: "existential for a fees product")

| Layer | Invariant examples | Verified by |
|-------|--------------------|-------------|
| Domain | Invoice totals = Σ lines; VAT rounding per line; payment ≤ outstanding; no negative balances | Property-based tests (FsCheck) in addition to example tests |
| Database | CHECK constraints, triggers per DB/04; gap-free numbering under concurrency | Integration tests incl. parallel-writer numbering test |
| Reconciliation | Nightly job: sub-ledger vs invoice/payment sums; ZATCA QR content vs invoice data | Job tested in integration; report asserted in E2E |

A dedicated **money test-fixture library** (currencies, VAT cases, Arabic-Indic rendering) is built in S0 (E-010) and mandatory for all finance tests.

## 4. Bilingual / RTL / accessibility gates

- **AR/EN screenshot gate** (per risk register): every screen ships with snapshots in both languages; diffs reviewed by bilingual QA. Runs per PR on changed screens, nightly full sweep.
- **PDF acceptance per language**: report cards, certificates, tax invoices, receipts each have AR and EN golden files; O6 engine output diffed against them (tolerance-based).
- **WCAG 2.1 AA**: axe-core automated scan in E2E on every screen + manual audit at E-803; keyboard map per docs/UI verified in E2E. **A11y/RTL defects are classified as defects** (not enhancements) — triaged with the same severity scheme as functional bugs.
- Hijri correctness: golden-date table (Umm al-Qura edge dates, year boundaries) as a domain test fixture.

## 5. Performance gates (from DB/04)

- Perf test suite runs against the **demo tenant at pilot scale** (≥1,500 students, 3 years of history, seeded by E-010's seeder — the same fixture sales uses).
- Gates at stage exits: S3 — attendance capture and cashier receipt < 2s p95 under 50 concurrent users; S7 — heavy reports run queued only, dashboard widgets < 3s p95; S8 — rollover completes on pilot-scale data within its maintenance window, resumable after kill.
- Query-plan review checklist for the 12 pivotal tables (DB/03) at each finance/attendance epic exit.

## 6. Defect policy & release criteria

- Severity S1 (data loss/financial error/security) blocks any deploy; S2 blocks stage exit; S3/S4 scheduled. A11y/RTL mapped into this scheme (S2 default for broken RTL layout on a core screen).
- **Release criteria per increment**: BR coverage green for shipped modules + zero S1/S2 + screenshot and PDF gates green + perf gates for that stage + migration rehearsal (upgrade from previous release on a copy of pilot DB, per M35 pre-op snapshot rules).

## 7. Gate IP-5 ask

Approve this quality plan; its costs are already inside the IP-4 ranges (test-writing in DW, QA roles in team shape). Proceed to IP-6 (release & pilot plan on scenario B).
