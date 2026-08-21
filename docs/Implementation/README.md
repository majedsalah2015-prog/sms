# Implementation Planning — Engagement Charter

**Engagement:** Implementation Planning & Estimation (successor to the analysis engagement, closed 2026-08-14)
**Input baseline:** Analysis v1.0 (all of `docs/` outside this folder) — changes to it go through the documented change-control paths, not through this engagement.
**Status:** ✅ All gates IP-0 through IP-7 approved 2026-08-14; Implementation Plan v1.0 baselined. **BUILD STARTED 2026-08-14 (T0 fixed by owner) — stage S0 in progress.** E-001 solution skeleton ✅ done: fresh repo `sms/` (P5), Clean Architecture projects, CI-enforced layer tests, `[BusinessRule]` trait, CI stub — build green, 5/5 tests pass, commit `6e41d27`. **CR-2 (owner directive):** runtime is **.NET 5** — superseding ADR-6/CR-1; EOL risk accepted, recorded in the risk register. Still open pre-T0: P3 (O6 PDF spike), P4 (hosting), P6 (pilot LOI), P7 (KSA-01 legal track). During build: stage-exit reviews land in `Reviews/`, amendments as addenda per 07 §4.
**Last updated:** 2026-08-14

---

## 1. Purpose

Turn the approved analysis into an executable plan: entry decisions resolved, technical foundation defined, work broken down against the recommended build order ([../Future/03-Final-Signoff.md](../Future/03-Final-Signoff.md) §5), estimated, staffed, and release-planned — ending with an explicit **approval to build**.

## 2. Ground rules

1. **No production code during planning.** The build starts only after Gate IP-7 (approval to build).
2. **Time-boxed technical spikes are allowed only with explicit approval per spike** (e.g., O6 PDF-engine RTL fidelity evaluation). Spike code is throwaway and never merges into the product.
3. Every phase ends with a review checkpoint awaiting explicit approval — same discipline as the analysis engagement.
4. Analysis v1.0 is the requirements authority. If planning uncovers a requirements defect, it is raised as a change request against the analysis baseline, not silently patched here.
5. Estimates are given as ranges with stated assumptions; no single-point commitments.

## 3. Entry checklist

The open-items register O1–O10 transfers from the sign-off package as this engagement's entry checklist — tracked in [00-Entry-Checklist.md](00-Entry-Checklist.md). None block the charter; each blocks the specific planning phase noted there. **O1 (country list) first — it unlocks the most.**

## 4. Phase plan

| Phase | Scope | Blocked by | Gate |
|-------|-------|-----------|------|
| **IP-0. Charter** | This document: phases, gates, ground rules | — | Gate IP-0 |
| **IP-1. Entry decisions** | Dispositions for O1–O10 (decided / deferred-with-assumption); assumptions baseline for everything still open | O1 drives most | Gate IP-1 |
| **IP-2. Technical foundation plan** | Solution skeleton (Clean Architecture layout), environments, CI/CD, branching, coding standards, library selections incl. reporting/PDF engine (O6), salary-encryption approach (O10) | O6, O10 | Gate IP-2 |
| **IP-3. Work breakdown** | Epics/stages mapped to build order §5; dependency graph; module → epic → BR traceability | IP-1 assumptions | Gate IP-3 |
| **IP-4. Estimation & team shaping** | Sizing model, estimate ranges per epic, roles, capacity, timeline scenarios | IP-3 | Gate IP-4 |
| **IP-5. Quality & test plan** | BR→test mapping approach (NF-M5), AR/EN screenshot gates, a11y/RTL/perf gates, demo tenant as QA fixture | IP-2 | Gate IP-5 |
| **IP-6. Release & pilot plan** | Increment plan (first sellable increment: Attendance + basic Grading + Fees/Payments), pilot-school program (O8), rollover rehearsal, country-pack content track | IP-1, O8 | Gate IP-6 |
| **IP-7. Consolidated plan** | Single consolidated implementation plan; carried risk register; **approval to build** | all prior | **Approval to build** |

## 5. Carried-over risk register

The six risks in the sign-off package §4 carry into this engagement unchanged and are re-assessed at IP-7.
