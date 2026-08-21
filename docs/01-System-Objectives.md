# 01 — System Objectives

**Phase:** 1 — Foundation | **Status:** Draft for review | **Owner:** Senior Business Analyst + QA Architect

---

## 1. Business objectives

| ID | Objective | Measure of success |
|----|-----------|--------------------|
| BO-01 | Eliminate fee leakage: every enrolled student has a complete, traceable fee position | 100% of active students carry fee schedules; aged receivables report reconciles to receipts |
| BO-02 | One permanent electronic student file across all academic years | Any authorized user retrieves full multi-year history in ≤ 3 clicks |
| BO-03 | Make year-end rollover routine | Promotion + re-registration + fee regeneration wizard, ≤ 1 day per school |
| BO-04 | Full accountability for sensitive operations | Discounts, refunds, mark changes, certificate issuance all workflow-approved and audited |
| BO-05 | Timely parent communication | Absence, result publication, and dues notifications delivered same day |
| BO-06 | Sellable product, not a project | New school onboarded by configuration only — zero code changes |
| BO-07 | Regulatory readiness | Ministry exports, VAT-compliant receipts/invoices, data-protection controls (per confirmed countries — README Q3) |
| BO-08 | Foundation for multi-school groups | All data school-scoped; consolidation reports feasible without schema change |

## 2. Functional objectives by domain

| Domain | Objective summary |
|--------|-------------------|
| Academic structure | Model school → academic year → semester/term → grade → section → classroom → subject → teacher assignment as configurable hierarchy |
| Admissions | Applicant pipeline (apply → review → approve → register) with waiting lists, sibling detection, automatic student numbering, class assignment, fee generation |
| Student & Parent | Permanent student file; deduplicated parent entity with N children; guardianship & custody handling |
| Attendance | Daily + period attendance; late/early-leave/permission/medical/excused/unexcused taxonomies; escalation rules |
| Timetable | Constraint-aware timetable (teacher availability, room capacity, subject loads); substitution management (identified as missing requirement — added) |
| Examinations & Grading | Exam types, schedules, marks entry with approval, configurable grading scales, GPA/ranking, transcripts, report cards |
| Certificates | Template-driven bilingual certificates with numbering, approval, and verification |
| Finance | Fee categories, installment plans, discounts/scholarships (approval-gated), receipts, refunds, late fees, VAT |
| Services | Transportation (routes/stops/bus attendance/fees), Health (medical file, vaccinations, visits), Discipline (incidents, actions, appeals), Library, Cafeteria, Store, Activities |
| HR | Employee files, contracts, qualifications, training, staff attendance & leave, payroll-preparation export |
| Platform | Reports catalog (150+), persona dashboards, internal messaging, notifications (in-app/email/SMS/WhatsApp-ready), audit, backup, administration |

Detailed functional requirements live in each module document (Phases 3–8).

## 3. Non-functional objectives

### 3.1 Performance & capacity

| ID | Requirement | Target |
|----|-------------|--------|
| NF-P1 | Students per school | 5,000 active (design ceiling 10,000) |
| NF-P2 | Concurrent users per school | 300 (peak: marks entry / result publication days) |
| NF-P3 | Screen response (interactive pages) | ≤ 2 s at P95 |
| NF-P4 | Attendance save per section | ≤ 1 s |
| NF-P5 | Standard report render | ≤ 10 s; heavy reports queued/async |
| NF-P6 | Historical data retained online | ≥ 10 academic years, no archival required for reporting |

### 3.2 Availability & recoverability

| ID | Requirement | Target |
|----|-------------|--------|
| NF-A1 | Availability during school hours | 99.5% (cloud offering) |
| NF-A2 | RPO | ≤ 15 min (cloud) / ≤ 24 h (on-prem default) |
| NF-A3 | RTO | ≤ 4 h |
| NF-A4 | Backup verification | Automated restore test — Backup module requirement |

### 3.3 Security & compliance

| ID | Requirement |
|----|-------------|
| NF-S1 | Role-based access with scope dimensions: module, screen, action, school, academic year, grade, section |
| NF-S2 | All access over TLS; passwords hashed (modern KDF); optional 2FA for admin/finance roles |
| NF-S3 | Field-level audit for sensitive entities (marks, fees, discounts, personal data) |
| NF-S4 | Data-protection compliance (PDPL/GDPR-style): consent, minimal retention, subject access — country list pending (README Q3) |
| NF-S5 | Student medical and discipline data restricted to need-to-know roles by default |
| NF-S6 | Tenant isolation: no query path can cross SchoolId boundaries |

### 3.4 Localization & usability

| ID | Requirement |
|----|-------------|
| NF-L1 | Full Arabic and English UI; user-selectable per session; per-user default |
| NF-L2 | Full RTL layout in Arabic (mirrored navigation, grids, forms, reports) |
| NF-L3 | Bilingual master data: every named entity stores NameAr + NameEn; documents print in either language |
| NF-L4 | Dual calendar display: Gregorian primary storage, Hijri display/entry where configured |
| NF-L5 | Time zone per school; all timestamps stored UTC, displayed in school time zone |
| NF-L6 | Responsive web UI (desktop-first for staff, mobile-friendly for teachers/parents) |
| NF-L7 | Accessibility target: WCAG 2.1 AA for portal-facing screens |

### 3.5 Maintainability & product qualities

| ID | Requirement |
|----|-------------|
| NF-M1 | Clean Architecture; module boundaries mirror the 36 business modules |
| NF-M2 | All school-specific behavior via configuration (no per-customer forks) |
| NF-M3 | Numbering formats, grading scales, calendars, templates, and workflows configurable per school |
| NF-M4 | Multi-tenant-ready data model from v1 (SchoolId on all tenant data) |
| NF-M5 | Automated test coverage for business rules; every numbered BR traceable to at least one test (QA gate at implementation) |

## 4. Constraints

| ID | Constraint | Note |
|----|-----------|------|
| C-1 | Stack: ASP.NET Core MVC, C#, SQL Server, EF Core, Clean Architecture, Bootstrap, jQuery | Version challenge raised: .NET 5 is EOL → recommend .NET 8 LTS (README Q1) |
| C-2 | Web application (no native apps in v1) | Portal is responsive web |
| C-3 | Analysis fully approved before any implementation | Engagement rule |
| C-4 | Single database platform (SQL Server) | Collation must support Arabic (e.g., Arabic_100_CI_AS or UTF-8) — Database phase decision |

## 5. Out of scope (v1)

Full payroll processing, general ledger accounting, LMS content delivery, native mobile apps, biometric hardware integration (interface-ready only), hostel/boarding, recruitment, alumni. All tracked in `Future/`.

## 6. Open questions

1. Confirm capacity targets NF-P1/P2 against the sales team's largest prospect.
2. Cloud offering SLA (NF-A1) — is 24/7 availability needed for parent portal, or school-hours weighted?
3. Is WCAG AA (NF-L7) contractually required anywhere, or best-practice target?
4. Plus README Q1–Q10.
