# Future 01 — GAP Analysis vs. Leading Commercial Systems

**Phase:** 12 | **Status:** Draft for review | **Owner:** Chief Solution Architect + Senior Business Analyst

> Benchmarked against the class leaders identified in the Vision (00 §6): **PowerSchool SIS** (global feature ceiling), **Classter** (mid-market SaaS breadth), **Classera** (MENA/Arabic benchmark), with Fedena/openSIS as breadth sanity checks. Assessment is feature-area level from public product documentation — a hands-on competitive teardown per named competitor is a sales-enablement task post-analysis.

**Verdicts:** ✅ Parity-or-better in v1 · ⚠️ Partial in v1 (rest on roadmap) · ❌ Gap (roadmap or accepted) · ➕ Our differentiator.

---

## 1. Feature-area matrix

| Area | v1 position | Verdict | Notes / disposition |
|------|-------------|---------|---------------------|
| Student information & e-file | 16-tab permanent file, multi-year, exemptions, custody | ➕ | Deeper than mid-market norm; parity with PowerSchool concepts |
| Parents as deduplicated entity, family billing | Payer model, merge tooling | ➕ | Weak spot in most competitors — sales lead point |
| Admissions & waitlists | Full pipeline, portal apply, offers | ✅ | CRM-lite only — see gap G6 |
| Academic structure & multi-year | GradeYearProfile versioning, rollover cockpit | ➕ | Rollover-as-workflow beats copy-year scripts common in market |
| Attendance | Daily+period, taxonomy, escalation, gate console | ✅ | Biometric/RFID hardware ❌ → R2 (interface-ready) |
| Timetable | Assisted-manual + validation + cover console | ⚠️ | **Auto-solver gap (G1)** vs aSc/Untis-integrated competitors; mitigations: import interim + R2 solver |
| Gradebook / continuous assessment | Blueprint components, marksheets | ⚠️ | Competitors with LMS-gradebooks offer richer formative tracking (G2 overlap) |
| Examinations & report cards | Rounds, seating, invigilation, snapshots, appeals | ✅ | Ministry-format packs pending country content |
| LMS (lessons, homework, online exams) | Out of v1 by decision (Q8) | ❌ | **G2 — the largest functional gap** vs Classera (LMS-first product); disposition: R3 module or partner integration; sales positioning: SIS/ERP depth first |
| Fees, installments, PDC, dunning | Full engine incl. PDC registry | ➕ | PDC + Gulf market fit exceeds global products' localization |
| Online payments | Gateway-dormant design | ⚠️ | **G3** — market expects pay-now; R1 priority activation |
| E-invoicing (ZATCA-class) | Structural readiness | ⚠️ | **G4** — KSA sales need live integration; R1/R2 per country list decision |
| Discounts/scholarships governance | Threshold ladders, envelopes, registers | ➕ | Governance depth uncommon in market |
| Portals (parent/student web) | Full-scope responsive portal | ✅ | Native apps ❌ → G5/R2; push notifications ride app delivery |
| Communication | Matrix-routed threads, letters w/ ack, announcements | ✅ | WhatsApp two-way ⚠️ → R2 |
| Transport | Routes, subscriptions, trip safety logs | ✅ | Live GPS tracking ❌ → R2 (parents increasingly expect the bus map) |
| Health, discipline, activities, library, cafeteria, store | Full modules | ➕ | Breadth beyond most SIS competitors (they stop at "infirmary notes") |
| HR & payroll | Employee file + payroll-prep export | ⚠️ | Full payroll ❌ by decision (Q7) → R3; acceptable vs SIS competitors (they don't either); local HR/payroll systems integrate via export |
| Security & audit | Scoped RBAC, field audit, integrity chains | ➕ | Enterprise-grade; exceeds mid-market SaaS norms |
| Reporting & dashboards | 228-report catalog, persona dashboards | ✅ | Self-service BI ❌ → R3 |
| Multi-school groups | Schema-ready, ops deferred | ⚠️ | By decision (Q2); R2 unlock — required before selling to chains |
| SSO / identity federation | Local accounts + 2FA | ❌ | G7 — Azure AD/Google SSO expected by larger schools → R2 |
| Ministry integrations (Noor-class) | Export formats per pack | ⚠️ | Live API integration per country → R2+ (dependent on ministry programs) |
| Arabic/RTL depth | Bilingual data model, RTL-first UI, Hijri | ➕ | Matches/exceeds Classera; far exceeds Western products |
| API / integration platform | Internal abstractions only | ❌ | G8 — public API expected by enterprise buyers → R2 |

## 2. Consolidated gap register

| # | Gap | Severity vs market | Disposition |
|---|-----|--------------------|-------------|
| G1 | Timetable auto-generation | Medium (schools adjust manually anyway; sales optics matter) | R2 solver; v1.x: aSc/Untis file import |
| G2 | LMS suite | High in MENA (Classera anchor) | R3 build-or-partner decision by end of R1 |
| G3 | Online payment execution | High | R1 (first post-GA release) |
| G4 | Live e-invoicing | High for KSA, else low | With country-pack decision (README Q1 finance) |
| G5 | Native mobile apps + push | Medium-high (parent expectation) | R2; portal PWA hardening in R1 as bridge |
| G6 | Admissions CRM (nurture, campaigns) | Low-medium | R3 |
| G7 | SSO | Medium (large schools/groups) | R2 |
| G8 | Public API/webhooks | Medium | R2 |
| G9 | GPS bus tracking | Medium | R2 with hardware partner |
| G10 | Self-service BI | Low | R3 |

## 3. Differentiator summary (sales narrative)

One sentence: **"Enterprise controls and true Arabic-first depth in a school-sized product"** — permanent student file, deduplicated family billing with PDC/Gulf finance reality, workflow-and-audit governance (discount ladders, mark-change control, integrity-chained audit), rollover-as-a-cockpit, and RTL/bilingual correctness competitors bolt on. The honest weaknesses to manage in sales: no LMS, no native apps at GA, assisted-manual timetabling.

## 4. Missing-requirements review (engagement rule: "identify missing requirements")

Captured and resolved during analysis: portal (Q6→v1) · substitution management (M15) · re-registration flow (M03) · Hijri (ADR-4) · VAT/e-invoicing readiness (M19) · data protection lifecycle (docs 06/07/10) · subject exemptions (M07→M10) · sponsor payers (M11→M19 abstraction) · PDC handling (M21) · sibling auto-discounts (M22) · wallet liability accounting (M27) · distribution tracking (M28) · consent hard-gate (M29) · warning letters (M14) · enrollment contract question (M09 Q1, pending country legal).

Remaining known unknowns are all country-pack content items (legal values, statutory formats) — listed in the sign-off package (doc 03 §3).
