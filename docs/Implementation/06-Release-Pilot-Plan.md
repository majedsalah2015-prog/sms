# IP 06 — Release & Pilot Plan

**Phase:** IP-6 | **Status:** ✅ **Gate IP-6 approved 2026-08-14** | **Basis:** scenario B (6 devs), stages S0–S8, KSA academic calendar
**Timeline anchor:** all dates assume **build start Oct 2026** (T0). The actual T0 is fixed at Gate IP-7; dates shift with it, sequence and boundaries do not.

---

## 1. Release train

| Release | Content | Target | Exit criteria (from IP-5 §6) |
|---------|---------|--------|------------------------------|
| **R-0.5 internal** | S0 walking skeleton + S1 academic structure | T0+4–5 mo (Feb–Mar 2027) | Arch/BR gates live; demo tenant configurable end-to-end; **re-estimation checkpoint (S1 exit)** |
| **R-0.9 pilot-ready** | + S2 people, S3 first sellable increment | T0+6.5–8.5 mo (Apr–Jun 2027) | Full IP-5 release criteria; ZATCA Phase-1 invoices verified against golden files; pilot-scale perf gates |
| **R-0.9.x pilot patches** | Fortnightly during pilot onboarding | Jul–Oct 2027 | Migration rehearsal before each |
| **R-1.0-beta** | + S4 academic ops, S5 finance completion, first S6 services | Dec 2027–Feb 2028 | Pilot runs semester-2 exams + full fee lifecycle on it |
| **R-1.0 GA** | + remaining S6, S7 platform, S8 hardening | **May–Aug 2028** | All gates + rollover rehearsal passed + O8 confirmations folded in |

Cadence: internal releases monthly; pilot receives releases only at semester-safe points (never during exam or invoicing windows — release calendar overlays the school calendar).

## 2. Pilot program

- **Selection criteria:** KSA private school, 800–2,000 students (inside perf-gate scale), AR-primary with EN section, willing to run **parallel operation** (old process + SMS) for attendance and fees during semester 1, names a full-time coordinator. One school; a second "shadow" school may receive read-only demos for feedback without support cost.
- **Onboarding (Jul–Aug 2027):** data migration from the school's current records via M36 imports with dry-run; **O8 confirmation workshop** — the ~25 flagged policy defaults walked through and set (output recorded as an addendum to this doc and fed back as change requests where a default proves wrong); staff training AR-first.
- **Pilot year (Sept 2027–Jun 2028):** semester 1 = attendance + fees live (parallel-run reconciliation weekly); semester 2 = exams/grading live, R-1.0-beta; year-end = **rollover rehearsal on a staging copy first (E-801), then the real rollover** — the pilot's successful year-end rollover is a GA release criterion.
- **Support model:** bilingual QA/BA is pilot liaison (per IP-4 capacity: ~15% of one dev + BA time); S1-severity hotline; feedback triaged into the standard defect/change-request flow — the pilot does not get bespoke features.

## 3. Country-pack & content track (parallel, non-developer)

Runs alongside S1–S3, owned by BA + legal + product owner — the roadmap's "parallel content track":

| When | Content |
|------|---------|
| During S1–S2 | KSA-01: holiday calendars, MoE age cutoffs, ID validation rules, leave matrices (Saudi Labor Law), behavior/discipline code list, vaccination schedule |
| Before R-0.9 | ZATCA invoice legal review (O4 confirmation); enrollment contract template + legal wording (O9); certificate-withholding legality opinion (BR-CRT-008) |
| During pilot | Noor export format validation against real ministry submissions; tax treatment of tuition confirmed with school's advisor |
| R2 planning | UAE pack scoping decision (revisit from IP-1 §O1) |

## 4. Commercial milestones alignment

- **Sellable from R-0.9**: sales can demo on the demo tenant and sign schools for Sept-2028 onboarding while the pilot proves the product; O5 SKU definition needed by E-704 (early 2028) to price GA contracts.
- **GA mid-2028** enters the 2028–29 academic-year sales cycle; second-school onboarding rehearses the repeatable onboarding playbook (drafted during pilot onboarding, refined at S8).
- Descope lever (from IP-3): individual S6 service modules can slip to R-1.1 without moving GA.

## 5. Gate IP-6 ask

Approve the release train, pilot program (incl. parallel-run and one-school scope), and content track. IP-7 then consolidates everything into the final plan and requests **approval to build**.
