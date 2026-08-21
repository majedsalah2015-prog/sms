# School Management System (SMS) — Enterprise Analysis & Design

**Status:** ✅ **Analysis engagement complete — final sign-off granted 2026-08-14.** All documentation is baselined as **Analysis v1.0**; subsequent changes go through the documented change-control paths. **Implementation planning:** ✅ complete — [Implementation Plan v1.0](Implementation/README.md) baselined 2026-08-14, **approval to build granted** (Gate IP-7). Build starts after pre-T0 checklist P1–P5 ([Implementation/07-Consolidated-Plan.md](Implementation/07-Consolidated-Plan.md) §3).
**Documentation owner:** Enterprise Architecture Team (virtual)
**Last updated:** 2026-08-14

---

## 1. How this documentation set works

- Every document is versioned Markdown inside `docs/`.
- Every module gets its own document under `docs/Modules/`.
- Database design (`docs/Database/`) is produced **only after** all module analyses are approved.
- Each phase ends with a **Review Checkpoint**. Work on the next phase does not start until the checkpoint is explicitly approved.
- Business rules are individually numbered (`BR-GLB-###` for global, `BR-<MOD>-###` per module) so they can be referenced from screens, validations, tests, and reports.
- Open questions are tracked per document and consolidated in §5 below.

## 2. Documentation map

| # | Document | Phase | Status |
|---|----------|-------|--------|
| 00 | [Project Vision](00-Project-Vision.md) | 1 | ✅ **Approved 2026-08-13** |
| 01 | [System Objectives](01-System-Objectives.md) | 1 | ✅ **Approved 2026-08-13** |
| 02 | [System Architecture](02-System-Architecture.md) | 1 | ✅ **Approved 2026-08-13** |
| 03 | [Business Rules (Global)](03-Business-Rules.md) | 1 | ✅ **Approved 2026-08-13** |
| 04 | [Glossary](04-Glossary.md) | 1 | ✅ **Approved 2026-08-13** |
| 05 | [Workflow Framework](05-Workflow.md) | 2 | ✅ **Approved 2026-08-13** |
| 06 | [Security Framework](06-Security.md) | 2 | ✅ **Approved 2026-08-13** |
| 07 | [Audit Framework](07-Audit.md) | 2 | ✅ **Approved 2026-08-13** |
| 08 | [Numbering Framework](08-Numbering.md) | 2 | ✅ **Approved 2026-08-13** |
| 09 | [Notifications Framework](09-Notifications.md) | 2 | ✅ **Approved 2026-08-13** |
| 10 | [Attachments Framework](10-Attachments.md) | 2 | ✅ **Approved 2026-08-13** |
| M01–M08 | [Modules 1–8: Academic structure](Modules/README.md) | 3 | ✅ **Approved 2026-08-13** |
| M09–M13 | [Modules 9–13: People](Modules/README.md) | 4 | ✅ **Approved 2026-08-13** |
| M14–M18 | [Modules 14–18: Academic operations](Modules/README.md) | 5 | ✅ **Approved 2026-08-13** |
| M19–M22 | [Modules 19–22: Finance](Modules/README.md) | 6 | ✅ **Approved 2026-08-13** |
| M23–M29 | [Modules 23–29: Student services](Modules/README.md) | 7 | ✅ **Approved 2026-08-13** |
| M30–M36 | [Modules 30–36: Platform](Modules/README.md) | 8 | ✅ **Approved 2026-08-13** |
| — | [`Reports/` — catalog (228 reports) + dashboard specs](Reports/README.md) | 9 | ✅ **Approved 2026-08-13** |
| — | [`Database/` — standards, ER model, ~190 tables, indexes](Database/README.md) | 10 | ✅ **Approved 2026-08-13** |
| — | [`UI/` — UI Design Guide (foundations, patterns, RTL/a11y)](UI/README.md) | 11 | ✅ **Approved 2026-08-13** |
| — | [`Future/` — GAP analysis, roadmap R1–R3, final sign-off](Future/README.md) | 12 | ✅ **Approved 2026-08-14 (final sign-off)** |

## 3. Phase plan

| Phase | Scope | Gate |
|-------|-------|------|
| **1. Foundation** | Vision, Objectives, Architecture, Global Business Rules, Glossary | Checkpoint 1 |
| **2. Cross-cutting frameworks** | Workflow, Security, Audit, Numbering, Notifications, Attachments (docs 05–10) | Checkpoint 2 |
| **3. Academic structure** | Modules 1–8: System Setup, Schools, Academic Years, Calendar, Grades, Sections, Subjects, Classrooms | Checkpoint 3 |
| **4. People** | Modules 9–13: Admissions, Students, Parents, Employees, Teachers | Checkpoint 4 |
| **5. Academic operations** | Modules 14–18: Attendance, Timetable, Examinations, Grading, Certificates | Checkpoint 5 |
| **6. Finance** | Modules 19–22: Fees, Installment Plans, Payments, Discounts | Checkpoint 6 |
| **7. Student services** | Modules 23–29: Transportation, Health, Discipline, Library, Cafeteria, Store, Activities | Checkpoint 7 |
| **8. Platform** | Modules 30–36: Reports, Dashboards, Messaging, Notifications, Audit, Backup, System Administration | Checkpoint 8 |
| **9. Reporting catalog** | Full report catalog (150+), dashboard specifications per persona | Checkpoint 9 |
| **10. Database design** | ERD, tables, relationships, indexes, constraints, naming standards | Checkpoint 10 |
| **11. UI standards** | Full UI Design Guide (navigation, RTL, accessibility, shortcuts) | Checkpoint 11 |
| **12. Quality & GAP** | GAP analysis vs. leading commercial systems, missing-feature review, final sign-off | Approval to implement |

## 4. Module template

Every module document in `docs/Modules/` follows the same skeleton:
Purpose · Scope · Business Rules · Workflow · User Roles · Permissions · Database Concept · Required Screens · Validation Rules · Reports · Dashboard Widgets · Notifications · Future Enhancements · Open Questions.

## 5. Consolidated open questions & challenged assumptions (Phase 1)

> **Checkpoint 1 decision (2026-08-13): Phase 1 approved.** Recommendations Q1–Q10 accepted as proposed: **.NET 8 LTS** (ADR-6 accepted), **multi-tenant-ready** single-tenant v1, **Gulf/MENA private K-12** first market (country list still to be confirmed for country packs), configurable grading scales, gateway-ready cashier payments, **portal in v1 scope**, payroll-preparation only, LMS deferred, GL export interface, AR+EN shipping languages.

Original decision table (retained for traceability):

| # | Question / challenged assumption | Recommendation |
|---|----------------------------------|----------------|
| Q1 | **Target runtime: .NET 5 was specified but reached end-of-life in May 2022** — no security patches; unacceptable for a commercial product sold to schools. | Use **.NET 8 LTS** (same ASP.NET Core MVC + EF Core stack; near-zero migration cost at analysis stage). |
| Q2 | **Deployment model:** on-premise per school, cloud single-tenant, or multi-tenant SaaS? Drives tenancy, backup, licensing, and update strategy. | Build **multi-tenant-ready** (SchoolId scoping everywhere) but ship v1 as single-tenant cloud/on-prem; enables the Multi-School future without rework. |
| Q3 | **Primary market / regulatory context:** Arabic+RTL implies MENA. Which countries first? Determines Hijri calendar support, VAT %, e-invoicing (e.g. ZATCA in KSA), ministry integrations (e.g. Noor), data-protection law (PDPL/GDPR), gender-segregated sections. | Assume **Gulf/MENA private K-12** first; confirm country list. |
| Q4 | **Curricula supported:** national, American, British (IGCSE), IB? Affects grading scales, transcripts, academic structure. | Design grading as **configurable scales** so any curriculum fits. |
| Q5 | **Online payments** (gateway, parent self-service) — in v1 or future? | v1: record payments at cashier; design Payments module gateway-ready. |
| Q6 | **Parent/Student portals and mobile apps** — the module list has no portal module, but Dashboards for parents/students are requested. | Add a **Portal (web, responsive)** to scope; native apps to `Future/`. |
| Q7 | **Payroll:** prompt says "payroll preparation" only. Full payroll (GOSI/WPS, payslips) is a large system by itself. | v1: payroll **preparation data** (attendance, leave, deductions export); full payroll in `Future/`. |
| Q8 | **LMS features** (homework, lessons, online exams) — explicitly out of scope for v1? | Keep out of v1; GAP analysis will size it for the roadmap. |
| Q9 | **Accounting integration:** SMS keeps a fee sub-ledger; does it integrate with a GL/ERP? | Design export interface (journal summary) — not a full GL. |
| Q10 | **Languages beyond Arabic/English** (French, Urdu…)? | Architecture supports N languages; v1 ships AR+EN. |

## 6. Ground rules (from the engagement brief)

1. Never skip analysis. 2. No database tables before business analysis is complete. 3. No UI before business rules are complete. 4. Always identify missing requirements. 5. Challenge assumptions. 6. Recommend international best practices. 7. Every phase ends with a review checkpoint.
