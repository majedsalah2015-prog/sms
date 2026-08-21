# IP 03 — Work Breakdown Structure

**Phase:** IP-3 | **Status:** ✅ **Gate IP-3 approved 2026-08-14** | **Inputs:** [../Future/03-Final-Signoff.md](../Future/03-Final-Signoff.md) §5 build order, [02-Technical-Foundation.md](02-Technical-Foundation.md)

> Stages S0–S8 realize the sign-off build order. Epics are the estimation and tracking unit (IP-4 estimates per epic). Traceability: every epic names its modules; module docs carry the numbered BRs; epic exit requires its BR-coverage green (NF-M5) plus the AR/EN screenshot gate on its screens.

---

## 1. Stage map

| Stage | Content | Depends on | Milestone |
|-------|---------|-----------|-----------|
| **S0** | Foundations & cross-cutting frameworks | — | Walking skeleton deployed to QA |
| **S1** | Core academic structure (M01–08) + setup wizard | S0 | A school can be fully configured |
| **S2** | People (M09–13) | S1 | Students enrolled, staff registered |
| **S3** | **First sellable increment**: Attendance + basic Grading + Fees/Payments + portal essentials | S2 | **v0.9 pilot-ready** |
| **S4** | Remaining academic ops (Timetable, Examinations, full Grading, Certificates) | S3 | Full academic cycle |
| **S5** | Finance completion (Installments, Discounts, dunning, PDC, GL export) | S3 | Full fee lifecycle |
| **S6** | Student services (M23–29) — **parallelizable** with S4/S5 | S2 | Services live |
| **S7** | Platform (M30–36) — report long tail, dashboards, messaging, admin | S4, S5 | Feature-complete v1.0 |
| **S8** | Hardening & pilot: rollover rehearsal, perf gates, a11y/RTL audit, O8 confirmations | S7 | **v1.0 GA** |

## 2. Epics

### S0 — Foundations (the frameworks of docs 05–10 become code exactly once)

| Epic | Content | Source docs |
|------|---------|-------------|
| E-001 | Solution skeleton, CI/CD, architecture tests, environments | IP-02 |
| E-002 | Tenancy + academic-year context (SchoolId filters, working-year shell) | 02 §4–5 |
| E-003 | Security framework: roles × verbs × scopes, deny-by-default, policy engine | 06 |
| E-004 | Audit framework: T0–T3 capture, tamper-evidence | 07 |
| E-005 | Workflow engine: patterns P1–P5, WF catalog runtime | 05 |
| E-006 | Numbering service incl. gap-free `usp_IssueNumber` | 08, DB/04 |
| E-007 | Notifications core: templates, events, channels (in-app/email; SMS/WhatsApp adapters stubbed) | 09 |
| E-008 | Attachments service: typed store, limits, virus-scan hook, permission inheritance | 10 |
| E-009 | Localization & calendars: AR/EN resources, RTL shell, Hijri service, TZ handling | 02 §6, UI |
| E-010 | Lookups/master-data framework + demo-tenant seeder harness | 02 §9 |
| E-011 | Background jobs infrastructure (Hangfire) + job admin surface | 02 T-6 |

### S1 — Academic structure

| Epic | Modules |
|------|---------|
| E-101 | M01 System Setup + setup wizard (spans S1 modules) |
| E-102 | M02 Schools, M03 Academic Years (incl. Preparation status; rollover *workflow shell* only — full rehearsal in S8) |
| E-103 | M04 Calendar, M05 Grades (GradeYearProfile), M06 Sections (effective-dated membership) |
| E-104 | M07 Subjects (CurriculumOffering), M08 Classrooms |

### S2 — People

| Epic | Modules |
|------|---------|
| E-201 | M09 Admissions (workflow, contract doc, KSA-01 content) |
| E-202 | M10 Students (incl. per-student subject exemptions), M11 Parents (payer abstraction, sponsors) |
| E-203 | M12 Employees, M13 Teachers (Always Encrypted salary fields land here) |

### S3 — First sellable increment ⭐

| Epic | Content |
|------|---------|
| E-301 | M14 Attendance (daily + period, school-TZ day boundaries) |
| E-302 | M17 Grading — *basic subset*: configurable scales, mark entry, simple report card (needs O6 engine decision) |
| E-303 | M19 Fees + M21 Payments core: fee generation, cashier receipts, **ZATCA Phase-1 tax invoices (QR)** |
| E-304 | Portal essentials: parent/student view of attendance, marks, fees; announcements read-only |
| E-305 | Pilot readiness: KSA-01 content pack v1 (ID types, holidays, VAT config), demo seed complete |

### S4–S7 — completion stages

| Epic | Content |
|------|---------|
| E-401 | M15 Timetable (manual + constraints; solver stays R2) |
| E-402 | M16 Examinations, M17 full Grading (moderation, mark-change workflow) |
| E-403 | M18 Certificates (bilingual, withholding rule per KSA legal check) |
| E-501 | M20 Installment Plans, PDC lifecycle |
| E-502 | M22 Discounts (approval chains), dunning, statements |
| E-503 | GL journal-summary export (O3 assumption) |
| E-601..607 | M23 Transportation, M24 Health, M25 Discipline, M26 Library, M27 Cafeteria, M28 Store, M29 Activities — one epic each, independently schedulable |
| E-701 | M30 Reports platform + catalog long tail (228 minus those shipped with their modules) |
| E-702 | M31 Dashboards (widget registry, 7 personas) |
| E-703 | M32 Messaging, M33 Notifications admin |
| E-704 | M34 Audit admin, M35 Backup (verified restores), M36 SysAdmin (imports dry-run, jobs, purges, license — O5 SKUs needed here) |

### S8 — Hardening

| Epic | Content |
|------|---------|
| E-801 | Rollover rehearsal on pilot-scale data (highest-risk workflow per risk register) |
| E-802 | Performance gates (DB/04 targets), read models/snapshots for heavy reports |
| E-803 | WCAG/RTL audit + fixes; PDF acceptance per language |
| E-804 | O8 pilot policy confirmations + resulting config |

## 3. Rules encoded in this breakdown

1. **Reports ship with their module** where operationally needed (attendance sheets with E-301, receipts with E-303); E-701 is the long tail only — prevents the "reports at the end" trap.
2. Each module epic includes its screens, validations, notifications, and module reports per the 14-section module doc — an epic is *done* when its module doc is fully realized, not when its happy path works.
3. S6 service epics are the parallelization buffer: they absorb team capacity while S4/S5 specialists work, and can be descoped from v1.0 GA individually if pilot dates demand (each is self-contained).
4. Open-item consumption points: O6 decision needed before E-302; O5 before E-704; O3 adapter choice at E-503; O8 at E-804.

## 4. Gate IP-3 ask

Approve stages S0–S8 and the epic decomposition as the estimation basis for IP-4.
