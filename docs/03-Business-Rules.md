# 03 — Business Rules (Global)

**Phase:** 1 — Foundation | **Status:** Draft for review | **Owner:** Senior Business Analyst

> These are **cross-module (global) rules**. Each module document (Phases 3–8) adds its own `BR-<MOD>-###` rules and may *tighten* — never contradict — a global rule. Every rule here must be traceable to validations, workflows, and tests during implementation.

---

## 1. Identity & master data

| ID | Rule |
|----|------|
| BR-GLB-001 | Every named master-data entity stores an Arabic name and an English name; both are mandatory before the record can be activated. |
| BR-GLB-002 | A person (student, parent, employee) exists **once** in the system regardless of how many roles or years they span. Person identity is year-independent. |
| BR-GLB-003 | Person uniqueness is checked on national ID / residency ID (Iqama) / passport number per configuration; a duplicate match blocks creation and offers linking instead. |
| BR-GLB-004 | A parent is an independent entity linked to one or more students; student registration must link to an existing parent or create one through the deduplication check (BR-GLB-003). |
| BR-GLB-005 | Master data referenced by any transaction can never be hard-deleted — only deactivated (soft delete / status change). |
| BR-GLB-006 | Deactivated master data disappears from selection lists but remains visible in historical records and reports. |
| BR-GLB-007 | Every entity carries creation and last-modification metadata (user, UTC timestamp) without exception. |

## 2. School & tenancy

| ID | Rule |
|----|------|
| BR-GLB-010 | Every tenant-owned record belongs to exactly one school (SchoolId); no cross-school data access except by explicit multi-school permission scope. |
| BR-GLB-011 | School-level configuration (currency, time zone, calendars, numbering formats, grading scales, working week) applies to all records of that school and is versioned by academic year where it can change over time. |
| BR-GLB-012 | The working week (e.g., Sun–Thu) and weekend days are school configuration, not assumptions. |

## 3. Academic year scoping

| ID | Rule |
|----|------|
| BR-GLB-020 | Every transactional record (enrollment, attendance, marks, fees, timetable, discipline, activity participation…) belongs to exactly one academic year. |
| BR-GLB-021 | Exactly one academic year per school is **Active**; one may be in **Preparation**; prior years are **Closed** (read-only) or **Archived**. |
| BR-GLB-022 | Posting transactions into a Closed year requires a dedicated permission and is always audited with a mandatory reason. |
| BR-GLB-023 | Year-end rollover (promotion, re-registration, fee generation) is executed only through the rollover workflow — never by direct data entry into the new year for existing students. |
| BR-GLB-024 | A student has at most one active enrollment (grade + section) per academic year per school. |

## 4. Statuses & lifecycle

| ID | Rule |
|----|------|
| BR-GLB-030 | Every workflow-managed entity has a defined status lifecycle; status transitions occur only through defined workflow actions (doc 05), each with permission and audit. |
| BR-GLB-031 | Draft records do not affect any other module (no fees from draft admissions, no notifications from draft messages). |
| BR-GLB-032 | Cancellation never deletes: a cancelled record keeps its number, its history, and a mandatory cancellation reason. |

## 5. Numbering & documents

| ID | Rule |
|----|------|
| BR-GLB-040 | Student numbers, receipt numbers, certificate numbers, and all official document numbers are system-generated per the Numbering framework (doc 08) — never manually assigned. |
| BR-GLB-041 | Financial document numbers (receipts, invoices, refund vouchers) are strictly sequential per school per series with **no gaps**; cancellation voids the number, it is never reused. |
| BR-GLB-042 | A generated official number is immutable for the life of the record. |

## 6. Dates, time & calendars

| ID | Rule |
|----|------|
| BR-GLB-050 | Dates are stored Gregorian; timestamps stored UTC; display follows school time zone and calendar configuration (Hijri display where enabled). |
| BR-GLB-051 | No transactional date may fall outside its academic year's start/end dates (module rules define narrow exceptions, e.g., admission applications before year start). |
| BR-GLB-052 | Attendance, timetable, and exam dates must respect the school calendar (working days, holidays, events) — module docs define override permissions. |

## 7. Money & finance (global invariants; details in Phase 6)

| ID | Rule |
|----|------|
| BR-GLB-060 | All amounts are stored in the school currency with defined rounding (per-currency decimal places); no floating-point money. |
| BR-GLB-061 | VAT treatment (rate, inclusive/exclusive) is school configuration; every financial document snapshots the rates used at posting time. |
| BR-GLB-062 | Posted financial documents are immutable — corrections happen by reversal/adjustment documents, never by editing. |
| BR-GLB-063 | Any discount, scholarship, fee exemption, write-off, or refund requires workflow approval per configured thresholds and is always audited. |
| BR-GLB-064 | A student's financial position (charges − discounts − payments) must be derivable and reconcilable at any date (statement of account). |

## 8. Security & privacy (global invariants; details in doc 06)

| ID | Rule |
|----|------|
| BR-GLB-070 | Deny by default: a user sees only modules, screens, actions, and data scopes explicitly granted. |
| BR-GLB-071 | Data scopes compound: permissions may be limited by school, academic year, grade, and section simultaneously (e.g., homeroom teacher = own sections only). |
| BR-GLB-072 | Medical, discipline, and financial-hardship data are restricted categories: excluded from general search/export and visible only to explicitly granted roles. |
| BR-GLB-073 | Parents/students see only their own family's data through the portal; no staff screens are reachable from portal accounts. |
| BR-GLB-074 | Personal data exports (lists with contact details, IDs) require an export permission and are audited. |

## 9. Audit (global invariants; details in doc 07)

| ID | Rule |
|----|------|
| BR-GLB-080 | Sensitive entities (marks, fees, discounts, personal data, permissions, certificates) are audited at field level: user, UTC time, old value, new value, reason where required. |
| BR-GLB-081 | Audit records are append-only; no role, including system administrator, can edit or delete them. |
| BR-GLB-082 | Security events (login success/failure, lockout, permission change, impersonation, export) are audited. |

## 10. Attachments & documents (global invariants; details in doc 10)

| ID | Rule |
|----|------|
| BR-GLB-090 | Attachments are typed (document type taxonomy per module), size- and format-restricted, and inherit the permissions of their owning entity. |
| BR-GLB-091 | Mandatory-document rules (e.g., birth certificate for admission) are configurable per school and enforced by the owning workflow before its approval step. |

## 11. Notifications (global invariants; details in doc 09)

| ID | Rule |
|----|------|
| BR-GLB-100 | Notifications are template-driven and bilingual; the recipient's preferred language selects the template variant. |
| BR-GLB-101 | Business events (absence, result publication, payment receipt, fee due, admission decision…) raise notification events; channel routing (in-app/email/SMS) is school configuration. |
| BR-GLB-102 | Bulk messaging to parents requires a dedicated permission; all sent communications are retained and auditable. |

## 12. Validation philosophy

| ID | Rule |
|----|------|
| BR-GLB-110 | Validation happens server-side always; client-side validation is a usability layer, never the enforcement layer. |
| BR-GLB-111 | Validation messages are bilingual, specific, and reference the field; blocking errors vs. warnings are distinguished (warnings can be overridden only with permission and are logged). |
| BR-GLB-112 | Referential choices are picked from lists, never free-typed (no free-text grade names inside a student record). |

## 13. Open questions

1. BR-GLB-041 gap-free numbering: confirm legal requirement per target country (drives whether voiding vs. strict sequence tables are needed).
2. BR-GLB-003: which identity documents are mandatory per country pack (national ID vs. Iqama vs. passport)?
3. Retention periods for audit and personal data after a student leaves (country data-protection law — README Q3).
4. Should warning-override (BR-GLB-111) require a typed reason globally or per module?
