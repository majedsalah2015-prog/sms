# IP 01 — Entry Decisions & Assumptions Baseline

**Phase:** IP-1 | **Status:** ✅ **Gate IP-1 approved 2026-08-14** — dispositions locked as the assumptions baseline | **Input:** [00-Entry-Checklist.md](00-Entry-Checklist.md), O1 decision (KSA first)
**Rule:** every item ends this phase as **Decided** (decision recorded) or **Deferred** (explicit assumption recorded + carried as risk). Regulatory statements below are working assumptions for planning; each is confirmed by legal/tech during the phase that consumes it.

---

## O1 — Launch market: **DECIDED — Saudi Arabia first** (product-owner decision, 2026-08-14)

Country pack **KSA-01** becomes the first (and v1-only) country pack. Planning consequences:

| Area | KSA-01 content |
|------|----------------|
| VAT | 15% standard rate; private-school tuition treatment confirmed with tax advisor (exemptions vary by service type) |
| E-invoicing | ZATCA (FATOORA) regime — see O2 |
| Data protection | **PDPL** — governs the retention schedules, consent, and cross-border rules already designed in the data-protection lifecycle |
| Ministry context | MoE / **Noor** export formats; instructional-day minimums per MoE calendar |
| Calendars | Hijri prominence (dual-calendar display already in the analysis); KSA public/school holidays |
| ID types | Saudi National ID, Iqama (residency), passport for non-residents |
| Structure | Gender-segregated sections supported (already in design); MoE age cutoffs for grade placement |
| Legal checks queued | Certificate-withholding legality (BR-CRT-008), enrollment contract (O9), employment/leave matrices per Saudi Labor Law |

**UAE is the recommended second pack** (R2 timeframe) — revisit at IP-6.

## O2 — E-invoicing: **DECIDED (recommendation) — Phase-1 compliance in v1 core, ZATCA Phase-2 integration in R1**

KSA schools issuing tax invoices must already meet ZATCA **Phase 1** (generation: compliant invoice content + QR code). Phase 2 (integration/clearance) applies in waves by revenue. Therefore: v1 ships ZATCA-compliant invoice documents (QR, required fields) as part of the finance core — this is *not* deferrable for KSA; the Phase-2 API integration remains an R1 roadmap item as already planned. Confirm wave applicability per pilot school's revenue during IP-6.

## O3 — GL export targets: **DEFERRED**

Assumption: v1 ships the generic journal-summary export (CSV/Excel, mapping table per the Module 22 design) with no named-system adapter. Target systems chosen during pilot onboarding (pilot school's ERP decides the first adapter). Risk carried: an exotic pilot ERP could add adapter work — low.

## O4 — Tax-invoice document identity: **DECIDED (recommendation, subject to O1 legal confirmation)**

Under the ZATCA regime the tax invoice is issued at **charge time** (the fee invoice), not at receipt: **simplified tax invoice** for parent (B2C) payers, **standard tax invoice** for sponsor/company (B2B) payers — the payer abstraction from Phase 6 already distinguishes these. Receipts remain non-tax documents acknowledging payment.

## O5 — License/subscription enforcement: **DEFERRED**

Assumption: per-student-per-academic-year subscription, tier SKUs TBD by commercial; Module 36's license design (license file + periodic online validation, grace behavior) is structurally sufficient for any tiering. SKU/tier definition is not needed until Module 36 build (late in the order). Owner: commercial.

## O6 — Reporting/PDF engine: **DEFERRED to IP-2 with an approved evaluation plan**

Criteria are fixed (RTL fidelity, Arabic fonts/shaping, tagged PDF). IP-2 will shortlist candidates (e.g., QuestPDF, Syncfusion, DevExpress Reporting — licensing weighed alongside fidelity) and run a **time-boxed spike** rendering two acceptance documents — an Arabic report card and a ZATCA-format tax invoice — per ground rule 2, the spike itself needs explicit approval at Gate IP-2.

## O7 — Hosting target: **DEFERRED to IP-2**

Assumption for planning: cloud deployment with **in-KSA data residency** (PDPL-driven) as the default posture; candidate regions/providers evaluated in IP-2 against residency, cost, and SQL Server availability. On-prem remains a supported deployment (per architecture) — its backup commercial model stays with commercial. Risk carried: residency requirement may narrow provider choice — medium, priced into IP-2.

## O8 — Pilot policy confirmations: **DEFERRED to IP-6 by design**

The ~25 "confirm with pilot" defaults ship as configured defaults; the IP-6 pilot plan includes a structured confirmation checklist for onboarding. No planning impact before IP-6.

## O9 — Enrollment contract: **DEFERRED with assumption**

Assumption: a signed enrollment contract is required/expected for KSA private schools; the Admissions design already supports contract generation + acknowledgment, so this is content (template + legal wording), not structure. KSA legal review confirms during pilot legal pass. Risk: low.

## O10 — Salary-field encryption: **DEFERRED to IP-2 with a leaning**

Leaning: **SQL Server Always Encrypted** for salary columns (keeps plaintext out of the DB engine and backups, aligns with the existing column-level sensitivity tiers) unless IP-2 finds it conflicts with needed server-side computations (payroll-prep aggregations) — in which case app-layer encryption with key management via the hosting platform's KMS. Decision lands in the IP-2 technical foundation plan.

---

## Gate IP-1 summary

| Item | Disposition |
|------|-------------|
| O1 | ✅ Decided — KSA first (KSA-01 pack) |
| O2 | ✅ Decided — ZATCA Phase 1 in v1 core; Phase 2 in R1 |
| O4 | ✅ Decided — tax invoice at charge; simplified B2C / standard B2B |
| O3, O5, O8, O9 | Deferred with recorded assumptions (low risk) |
| O6, O7, O10 | Deferred **into IP-2** where they are decided |

Approving Gate IP-1 locks these dispositions as the assumptions baseline for IP-2 onward.
