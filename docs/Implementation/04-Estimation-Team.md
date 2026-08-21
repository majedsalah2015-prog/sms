# IP 04 — Estimation & Team Shaping

**Phase:** IP-4 | **Status:** ✅ **Gate IP-4 approved 2026-08-14 — scenario B (Standard, 6 devs) chosen** | **Input:** [03-Work-Breakdown.md](03-Work-Breakdown.md)

> Per charter ground rule 5: **ranges with stated assumptions, never single-point commitments.** Unit = developer-week (DW): one developer, one effective week, *including* that epic's unit/integration tests, screens, validations, module reports, and AR/EN localization (bilingual/RTL overhead ~15% is inside each range, not added later). QA, BA, PM effort is carried by team shape, not DW.

---

## 1. Sizing by stage (sums of per-epic ranges)

| Stage | Epics | DW low | DW high | Notes |
|-------|-------|--------|---------|-------|
| S0 Foundations | E-001..011 | 39 | 63 | Security (E-003) and workflow engine (E-005) dominate: 6–10 DW each |
| S1 Academic structure | E-101..104 | 19 | 29 | Setup wizard spans the stage |
| S2 People | E-201..203 | 20 | 30 | Admissions workflow + payer abstraction are the heavy parts |
| S3 ⭐ First sellable | E-301..305 | 30 | 47 | E-303 (fees/payments + ZATCA + financial invariants) largest single epic: 10–15 DW |
| S4 Academic ops | E-401..403 | 18 | 29 | Examinations + full grading moderation |
| S5 Finance completion | E-501..503 | 12 | 20 | PDC lifecycle, dunning |
| S6 Services (parallel) | E-601..607 | 21 | 42 | 3–6 DW each; independently descopeable |
| S7 Platform | E-701..704 | 26 | 42 | Report long tail (E-701) 8–14 DW |
| S8 Hardening | E-801..804 | 13 | 20 | Rollover rehearsal, perf, a11y/RTL audit |
| **Total** | ~35 epics | **198** | **322** | ≈ 46–74 developer-months |
| **To v0.9 pilot-ready** (S0–S3) | | **108** | **169** | The number that matters for pilot planning |

**Confidence:** these are analysis-stage ranges. A mandatory **re-estimation checkpoint at S1 exit** recalibrates the remaining stages against actuals (velocity, RTL overhead reality) — expect the spread to halve there.

## 2. Team shapes (scenarios)

Common core in all scenarios: 1 tech lead (hands-on, owns architecture tests + security/workflow engines), 1 QA automation engineer (owns BR-coverage gate + Playwright), 1 bilingual QA/BA (owns AR/EN screenshot gate + pilot liaison + O8), fractional UI/UX (RTL) and DevOps, PM.

| | **A — Compact** | **B — Standard (recommended)** | **C — Aggressive** |
|---|---|---|---|
| Developers (incl. lead) | 4 | 6 | 9 |
| Effective DW/year¹ | ~160 | ~240 | ~350 |
| v0.9 pilot-ready | 8–12 months | **5.5–8.5 months** | 4–6 months |
| v1.0 GA | 16–24 months | **11–16 months** | 8–12 months |
| Risks | Long time-to-revenue; key-person risk; S6 serializes | Balanced; S6 runs parallel on 2 devs while finance/academic specialists finish S4/S5 | Coordination overhead on shared foundations; risk-register warning about big-bang pressure; onboarding drag in S0 (foundations don't parallelize 9-wide) |

¹ ~40 effective weeks/dev/year (focus factor ~0.75 on 52 weeks, net of leave/meetings).

Scenario C caution: S0 has limited parallelism (~4–5 workstreams). A 9-dev team idles or builds modules on unstable foundations. If C is chosen, stagger onboarding: start 5, add 4 at S1.

## 3. Calendar reality (KSA academic year)

The pilot must start at a natural academic boundary: **year start (late Aug/Sept)** or **semester 2 (January)**. Working back with scenario B and a build start of **Oct 2026**:

- v0.9 pilot-ready: **Apr–Jun 2027** → buffer + pilot onboarding → **pilot at year start Sept 2027** (semester-2 Jan 2028 as fallback).
- S4–S7 built *during* the pilot year; pilot's first year-end rollover (May–Jun 2028) is preceded by the E-801 rehearsal — this sequencing is mandatory (risk register).
- **v1.0 GA: mid-2028**, entering the sales cycle for academic year 2028–29.

Scenario A pushes pilot to Sept 2028 (a full year of lost market time); scenario C could hit Jan 2027 semester-2 pilot but with S3 barely cooled — Sept 2027 with more polish is likelier in practice.

## 4. Estimation assumptions & exclusions

1. Analysis v1.0 stable; change requests are re-estimated, not absorbed.
2. Excluded (roadmap): native apps, LMS, payroll, country packs beyond KSA-01, payment-gateway integration (design-ready only), multi-school operation.
3. O6 engine decision lands before E-302 (spike already authorized); a forced engine switch later would cost 2–4 DW rework.
4. Hosting per IP-2 posture; cloud onboarding/commercial setup not in DW.
5. Pilot school support during S4–S7 costs ~15% of one developer + the bilingual QA/BA — included in scenario capacity.

## 5. Gate IP-4 asks

1. Approve the sizing model and ranges as the planning basis (re-estimation checkpoint at S1 exit).
2. **Choose a team scenario** (A / B / C) — this fixes the timeline skeleton for IP-6's release plan.
