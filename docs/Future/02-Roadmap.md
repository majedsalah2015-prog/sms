# Future 02 — Product Roadmap

**Phase:** 12 | **Status:** Draft for review | Consolidates every module's Future Enhancements section + the GAP register (doc 01) into sequenced releases. Sequencing drivers: sales-blocking gaps first (G3/G4/G5), then platform multipliers (multi-school, API, SSO), then adjacent suites (LMS, payroll).

---

## R1 — first post-GA release cycle (v1.x)

| Item | Source |
|------|--------|
| **Online payment gateway activation** (regional PSP, portal pay-now, auto-receipt/allocation, settlement reconciliation) | G3, BR-PAY-007 dormant design |
| E-invoicing live integration where country list demands (ZATCA-class) | G4, BR-FEE-005 |
| Portal PWA hardening (installable, offline shell, web push where supported) | G5 bridge |
| Parent self-upload of documents with verification queue (re-registration season relief) | doc 10 Q2 |
| Sponsor/company payer activation (sponsor entities, statements, bulk invoicing) | BR-FEE-004, M11 Q1 |
| Early-payment discount type | M22 Q4 |
| Timetable import (aSc/Untis file formats) | G1 interim |
| Same-day emergency closure flow | M04 Q3 |
| Barcode student ID at cafeteria/library POS | M27 Q1 |
| Cafeteria allergy hard-block option | M24 Q5 / M27 Q2 |

## R2 — platform multipliers

| Item | Source |
|------|--------|
| **Multi-school group operations**: tenant provisioning console, group roles/scopes, consolidated dashboards & reports, cross-school transfer workflow, shared parent accounts | BR-SCH-007, Q2 decision, M31/M36 futures |
| **Native mobile apps** (parent first, teacher second) with push notifications | G5 |
| **SSO** (Azure AD / Google Workspace) for staff | G7 |
| **Public API + webhooks** with key management | G8, M36 future |
| Timetable auto-generation solver (shared constraint engine with exam seating optimization) | G1, M15/M16 futures |
| GPS/telematics bus tracking with parent live map + RFID tap-on/off | G9, M23 futures |
| WhatsApp Business two-way threads + interactive approvals | M32/doc 09 futures |
| Biometric/RFID attendance devices (students + staff) | M14/M12 futures |
| SADAD-class national bill presentment (KSA) | M21 future — high sales value |
| Ministry live API integrations (Noor-class) per country program availability | GAP §1 |
| External-auditor time-boxed access + attestation packages | M34 futures, doc 06 Q3 |
| Customer off-boarding data export (contractual) | M35 future |

## R3 — adjacent suites

| Item | Source |
|------|--------|
| **LMS suite decision executed** (build vs partner — decision due end of R1): homework, lesson plans, online exams w/ question banks, OMR scanning | G2, M16 futures |
| **Full payroll add-on** (GOSI/WPS, payslips, end-of-service) + appraisal cycles + recruitment | Q7, M12 futures |
| Self-service BI (semantic model, embedded analytics) + data warehouse for groups | G10, M30 futures |
| Admissions CRM (inquiry nurturing, campaigns, forecasting) | G6, M09 futures |
| Alumni module (transcript self-service, community) | M10 futures |
| Counseling/wellbeing module (ultra-restricted governance tier) | M24 Q3 |
| Behavior/welfare early-warning composite signals (governance-reviewed) | M25/M10 futures |
| Elective/option groups scheduling + student subject selection | BR-SUB-008, M06/M15 |
| Merit-points redemption store; house system | M25/M28/M29 futures |
| Cafeteria pre-ordering; supplier/procurement modules (store+cafeteria) | M27/M28 futures |
| Template designer for school-designed certificates/report cards | BR-SCH Q3, M18 future |
| Explicit-deny permissions; field-level permission UI | doc 06 future |
| SIEM export; ML audit anomaly detection | doc 07/M34 futures |

## Governance

- Roadmap items graduate only through the same discipline: module-doc amendment → rules → screens → catalog/database change control (README rules, catalog/inventory-first).
- Each release cycle re-runs a GAP checkpoint against the market (doc 01 refresh) — the competitor set moves.
- Country packs are a **parallel content track** (not release-bound): legal values, statutory formats, behavior codes, vaccination schedules, leave matrices per confirmed market entry.
