# IP 07 — Consolidated Implementation Plan & Approval to Build

**Phase:** IP-7 | **Status:** ✅ **Gate IP-7 APPROVED 2026-08-14 by the Product Owner — approval to build granted; Implementation Plan baselined v1.0**
**This document consolidates IP-1..IP-6; it introduces nothing new.** On approval, the planning engagement closes and the build engagement may start once the pre-T0 checklist (§3) is done.

---

## 1. The plan on one page

- **Product:** bilingual (AR/EN, RTL) School Management System, 36 modules, Clean Architecture modular monolith on .NET 8 / SQL Server (Analysis v1.0 = requirements authority).
- **Market:** Saudi Arabia first (KSA-01 pack; ZATCA Phase 1 in v1 core, Phase 2 in R1; PDPL residency posture). UAE pack revisited at R2.
- **Team:** scenario B — 6 developers (incl. hands-on tech lead), QA automation, bilingual QA/BA, fractional UI/UX + DevOps, PM.
- **Effort:** 198–322 DW total; 108–169 DW to v0.9 pilot-ready; mandatory re-estimation at S1 exit.
- **Sequence:** S0 foundations → S1 academic structure → S2 people → **S3 first sellable increment** (attendance, basic grading, fees/payments + ZATCA, portal) → S4/S5 academics+finance completion with S6 services in parallel → S7 platform → S8 hardening.
- **Releases (T0 = build start, assumed Oct 2026):** R-0.5 internal ~T0+5mo → **R-0.9 pilot-ready ~T0+7mo** → pilot at next semester boundary (Sept 2027) → R-1.0-beta winter → **GA May–Aug 2028**.
- **Pilot:** one KSA school (800–2,000 students), parallel-run semester 1, exams semester 2, year-end rollover (rehearsed first) as GA criterion.
- **Quality:** BR-coverage gate (NF-M5) in CI; three-layer financial-integrity testing; AR/EN screenshot + PDF golden-file gates; WCAG AA; perf gates at pilot scale; migration rehearsal before every pilot/production deploy.

## 2. Risk register — re-assessed at IP-7

| Risk (from sign-off §4) | Status after planning |
|--------------------------|----------------------|
| Big-bang temptation | Mitigated structurally: staged releases, first sellable increment at S3, S6 descope lever |
| RTL/bilingual erosion | Gated in CI (screenshots, PDF golden files, S2-severity policy) — cost priced into DW |
| Financial-integrity bugs | Three test layers + property-based tests + nightly reconciliation; ZATCA content verified against golden files |
| Country-pack content lag | Parallel non-dev content track with named owners and dates (IP-6 §3) |
| LMS competitive pressure | Unchanged — G2 decision deadline end of R1 (commercial watch item) |
| Rollover complexity | Rehearsal-then-live in pilot year; resumability a perf-gate criterion |
| **New — hiring risk** | Scenario B needs 6 devs incl. a strong tech lead by T0; late hiring shifts T0 and therefore the Sept-2027 pilot boundary to Jan-2028. Mitigation: hire lead + 2 seniors first, they build S0's critical epics (E-002/003/005) |
| **New — single-pilot dependency** | One school's calendar/cooperation gates GA evidence. Mitigation: shadow school for feedback; GA criteria phrased against gates, not pilot sentiment |
| **New — EOL runtime (CR-2)** | Owner directive: build on .NET 5 (EOL May 2022, no security patches). **Accepted risk, owner-signed 2026-08-14.** Mitigations: keep upgrade path open (no removed-API reliance where practical); compensate at perimeter (WAF/reverse proxy recommended at deployment); revisit before pilot exposure to the internet (portal) |

## 3. Pre-T0 checklist (blocks build start, not this approval)

| # | Item | Owner |
|---|------|-------|
| P1 | Fix actual T0 (hiring-dependent); recompute IP-6 dates from it | Product owner + PM |
| P2 | Hire/assign scenario-B team (lead + 2 seniors first) | Product owner |
| P3 | Execute the authorized O6 PDF-engine spike; record decision as addendum to IP-02 §4 | Tech lead |
| P4 | Hosting provider selection per IP-02 §5 (in-KSA residency) with commercial | Commercial + tech lead |
| P5 | Initialize the fresh build repository + CI per IP-02 (nothing reused from the current workspace without review) | Tech lead |
| P6 | Engage pilot-school candidates against IP-6 §2 criteria (LOI before R-0.9) | Product owner |
| P7 | Start KSA-01 content/legal track (IP-6 §3 — ZATCA legal review first) | BA + legal |

## 4. Standing governance during build

- Analysis v1.0 change control: defects/changes → change requests against `docs/`, re-estimated (never silently absorbed).
- Stage-exit reviews replace phase gates: each S-stage exits against IP-5 criteria with a short written review in this folder (`Reviews/S<0-8>-exit.md`).
- Open-item consumption points (O3, O5, O6, O8) tracked in [00-Entry-Checklist.md](00-Entry-Checklist.md) until each is closed.
- Re-estimation at S1 exit updates IP-04 and IP-06 as addenda — baseline documents are amended, not forked.

## 5. Gate IP-7 — approval to build

Approving this gate:

1. Baselines `docs/Implementation/` as **Implementation Plan v1.0**.
2. Closes the implementation-planning engagement (per its charter ground rules).
3. Grants **approval to build**: production code may be written once pre-T0 items P1–P5 are complete (P6–P7 run in parallel with early build).

**Approval requested from:** Product Owner.
