# Module 07 — Subjects

**Phase:** 3 — Academic structure | **Status:** Draft for review | **Rule prefix:** `BR-SUB`

---

## 1. Purpose

Maintain the subject catalog and the **curriculum plan** — which subjects each grade studies in each academic year, with weekly periods, assessability, and weights — feeding Timetable (loads), Examinations/Grading (assessable subjects, weights), and Teacher Assignment (Module 13).

## 2. Scope

**In:** subject catalog (bilingual, coded, categorized), curriculum plan per grade-year (offerings: weekly periods, assessable flag, GPA weight/credit, pass criteria ref), subject grouping (department), teacher-qualification linkage (what a teacher may teach), elective placeholder (future-safe).
**Out:** timetable placement (Module 15), grading scales & mark templates (Module 17), teacher entity (Modules 12/13), lesson content (LMS — out of v1).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-SUB-001 | Subjects are school-catalog entries: unique code, bilingual names, category (core/language/religious/arts/PE…), department link; product ships country-pack starter catalogs. |
| BR-SUB-002 | A **curriculum plan** exists per grade-year profile (BR-GRD-008 versioning): the set of subject offerings with weekly periods, assessable flag, GPA weight, and optional pass-mark reference (criteria detail in Module 17). |
| BR-SUB-003 | Non-assessable offerings (e.g., assembly, homeroom period) appear in timetable but never in marks entry or GPA (Module 15 schedules them; Module 17 ignores them). |
| BR-SUB-004 | An offering referenced by marks, timetable sessions, or teacher assignments cannot be removed from the plan — only end-dated for future terms (P2, Principal) with impact display; historical years immutable. |
| BR-SUB-005 | Weekly-period totals per grade are validated against the school's periods-per-week (timetable shape, Module 15 config): plan total ≤ available slots; live counter while editing. |
| BR-SUB-006 | Teacher–subject **qualification matrix**: which teachers are qualified per subject (+ optional stage restriction) — maintained here (HR data feed, Module 12 qualifications) and consumed by Teacher Assignment (Module 13) and substitution (Module 15) as a hard or soft rule (school-configurable strictness). |
| BR-SUB-007 | Departments (subject groups) exist for Head-of-Department scoping (WF-07 marks approval chain) and reporting; a subject belongs to at most one department. |
| BR-SUB-008 | Elective groups are modeled as a plan-level placeholder in v1 (offering flagged Elective with a group tag) but student-level option selection is deferred (BR-SCN Q1 coordination) — the data model must not preclude it (Phase 10 note). |

## 4. Workflow

Catalog editing: direct (audited T3). Curriculum plan: Draft (Preparation year) → Confirmed at year activation (part of BR-AYR-004 checklist); active-year plan changes = P2 (Principal) with impact panel (BR-SUB-004). Qualification matrix: direct by HR/Academic admin (T2).

## 5. User roles

Registrar / Academic Deputy (plan author), Head of Department (own-department view + proposals), Principal (active-year plan approver), HR Officer (qualification feed), Sys Admin (catalog).

## 6. Permissions

| Action | Roles |
|--------|-------|
| View catalog/plans | All academic staff (scope) |
| Edit catalog | Sys Admin, Registrar |
| Edit Preparation-year plan | Registrar, Academic Deputy |
| Change active-year plan | + Principal approval (P2) |
| Edit qualification matrix | HR Officer, Academic Deputy |
| Manage departments | Sys Admin |

## 7. Database concept

Entities: `Subject` (school, code, bilingual, category, department ref); `Department` (bilingual, head-teacher ref per year); `CurriculumOffering` (grade-year profile × subject: weekly periods, assessable, weight, elective flag, effective dates); `TeacherSubjectQualification` (teacher × subject × optional stage, source: qualification/approval). Offerings are the reference target for timetable sessions, marksheets, and assignments — never raw Subject (year-correctness by construction).

## 8. Required screens

1. Subject catalog — grid with category/department filters, usage indicators.
2. **Curriculum plan editor** — per grade-year: offerings grid (subject, periods/week, assessable, weight, elective), live period-total vs available (BR-SUB-005), copy-from-previous-year, copy-across-grades.
3. Department manager — subjects per department, head assignment per year.
4. Qualification matrix — teacher × subject grid with stage restrictions, bulk edit, gaps highlight (subjects with < N qualified teachers).
5. Plan change request (active year) — impact panel + P2 submission.

## 9. Validation rules

Unique subject codes; offering uniqueness (grade-year × subject); periods/week ≥ 1 for scheduled offerings; assessable offerings must carry weight > 0; plan-total validation (BR-SUB-005) blocking at confirmation, warning while drafting; department head must be a qualified teacher in that department; removal attempts route to end-dating flow (BR-SUB-004).

## 10. Reports

Curriculum plan sheet per grade (bilingual — ministry/parent handbook) · Weekly-period distribution per grade (by category: religious/languages/sciences %) · Qualification coverage report (subjects vs qualified-teacher count — hiring signal) · Plan change register · Department composition sheet.

## 11. Dashboard widgets

Academic Deputy: plans confirmed vs pending per grade (pre-activation), qualification gaps count. HoD: my department's offerings and assigned teachers (Module 13 feed). Principal: curriculum load summary per stage.

## 12. Notifications

`PlanConfirmed` → HoDs, Timetable owner; `PlanChangedActiveYear` → affected teachers, Timetable owner, Exams officer; `QualificationGap` (subject below threshold) → HR, Academic Deputy.

## 13. Future enhancements

Student-level elective selection with seat caps (with Modules 06/15); syllabus/pacing attachments per offering (pre-LMS); cross-listed subjects for dual-curriculum tracks; textbook mapping per offering (feeds Store module bundles).

## 14. Open questions

1. GPA weight semantics (credit hours vs percentage weights) differ per curriculum — Module 17 must define scale-level semantics; offering stores a neutral numeric weight. Confirm this division.
2. Qualification strictness default: hard block or warning on unqualified assignment? Recommendation: **warning + permission-gated override** (logged); confirm.
3. Do religious-education exemptions (non-Muslim students exempt from Islamic studies) need per-student subject exemptions in v1? **Identified missing requirement** — recommend yes: per-student offering exemption flag consumed by Attendance/Grading; carried to Module 10 scope.
