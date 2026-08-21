# IP 00 — Entry Checklist (Open Items O1–O10)

**Source:** [../Future/03-Final-Signoff.md](../Future/03-Final-Signoff.md) §3 — transferred at sign-off (2026-08-14).
**Status:** ✅ **Gate IP-1 approved 2026-08-14** — all dispositions locked per [01-Entry-Decisions.md](01-Entry-Decisions.md). Decided: O1 (KSA first), O2 (ZATCA Phase 1 in v1 core, Phase 2 in R1), O4 (tax invoice at charge; simplified B2C / standard B2B). Deferred with assumptions: O3, O5, O8, O9. Deferred into IP-2 for decision: O6, O7, O10.

Each item is resolved as **Decided** (decision recorded here) or **Deferred** (explicit assumption recorded and carried as risk). Gate IP-1 requires every item to be one or the other — no silent unknowns.

| # | Item | Blocks (planning phase / build stage) | Owner | Status | Disposition |
|---|------|--------------------------------------|-------|--------|-------------|
| O1 | Country list confirmation → unlocks country-pack content (VAT/e-invoicing regime, legal retention, ID types, age cutoffs, leave matrices, behavior codes, vaccination schedules, ministry formats, certificate-withholding legality, instructional-day minimums) | IP-1, IP-6; finance/certificates/statutory build | Product owner | ✅ **Decided 2026-08-14** | **Saudi Arabia first** — country pack KSA-01; see [01-Entry-Decisions.md](01-Entry-Decisions.md) §O1 |
| O2 | E-invoicing live vs readiness for launch market | IP-1; finance build | Product owner (+O1) | ⏳ Open | — |
| O3 | GL export target systems (fixes file format) | Finance build | Product owner | ⏳ Open | — |
| O4 | Tax-invoice document identity per regime (charge vs receipt) | Template design | O1 legal | ⏳ Open | — |
| O5 | License/subscription enforcement model (tiers, SKUs) | Module 36 build | Commercial | ⏳ Open | — |
| O6 | Reporting/PDF engine selection (criteria fixed: RTL fidelity, Arabic fonts, tagged PDF) | IP-2; report card/certificate build | Tech lead | ⏳ Open — QuestPDF spike run 2026-08-18 (E-803): **fails bidi/digits on .NET 5**, see [02 §4 addendum](02-Technical-Foundation.md#4-o6--pdfreporting-engine-shortlist--spike-approval-requested-at-this-gate) | — |
| O7 | Cloud hosting target + on-prem backup commercial model | IP-2; deployment design | Commercial + tech | ⏳ Open | — |
| O8 | Pilot-school confirmations (~25 low-risk policy defaults marked "confirm with pilot") | IP-6; pilot onboarding | BA with pilot | ⏳ Open | — |
| O9 | Enrollment contract legal requirement | Admissions build | O1 legal | ⏳ Open | — |
| O10 | Salary-field encryption approach (Always Encrypted vs app-layer) | IP-2; HR build | Tech lead | ⏳ Open | — |
