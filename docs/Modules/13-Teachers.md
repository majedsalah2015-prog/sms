# Module 13 — Teachers

**Phase:** 4 — People | **Status:** Draft for review | **Rule prefix:** `BR-TCH`

---

## 1. Purpose

The academic overlay on the employee file: teacher profiles, **subject–section assignments** per academic year (the load that drives timetable, marks entry rights, and "own sections/subjects" permission scopes), load management, and availability for scheduling.

## 2. Scope

**In:** teacher designation (which employees teach), teacher academic profile, teacher assignments (teacher × offering × section × year), load rules & balancing, availability constraints (for Module 15), homeroom linkage view (Module 06 owns), teacher workspace (my classes/timetable/marksheets entry points).
**Out:** employee HR data (Module 12), timetable generation (Module 15), marks entry itself (Modules 16/17), qualification records (Module 12 — consumed via BR-SUB-006 matrix).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-TCH-001 | A **teacher** is an employee flagged Teaching with an active contract (BR-EMP-003); losing the flag or contract end-dates all assignments forward (with impact flow to Module 15). |
| BR-TCH-002 | **Assignment** = teacher × curriculum offering × section × academic year (referencing `CurriculumOffering` per BR-SUB model): the atomic unit granting marks-entry rights (Module 17), attendance-entry rights for that class (Module 14 period mode), and dynamic scopes (doc 06 §4.2). Effective-dated (mid-year teacher changes preserve history — marks/attendance recorded remain with the recorded teacher). |
| BR-TCH-003 | Assignment validates against the **qualification matrix** (BR-SUB-006) at configured strictness (warn + permission override default, per Module 07 Q2), and against gender policy where school config restricts cross-gender teaching per stage (market reality; configurable, off by default). |
| BR-TCH-004 | **Load rules:** each teacher has a max weekly periods target (per contract type, school config; e.g., 24); assignment totals (offering periods × sections) compute live load; exceeding max requires override permission (logged) — under-loading is reported, not blocked. |
| BR-TCH-005 | One offering × section = one primary teacher at a time; optional co-teacher/assistant roles (view + attendance rights, no marks approval) — supports lab assistants and KG co-teachers. |
| BR-TCH-006 | **Availability constraints** per teacher per year: unavailable slots (approved part-time patterns, nursing-hour entitlements per labor law pack, cross-campus days) — Module 15 consumes as hard constraints; constraint entry is HR/Deputy-controlled (not self-service) to prevent scheduling gridlock. |
| BR-TCH-007 | Mid-year reassignment runs the continuity flow: open marksheets transfer to the new teacher (Module 17 consumes), timetable sessions re-point (Module 15), parents of affected sections notified (school-configurable). P2 approval (Academic Deputy). |
| BR-TCH-008 | Assignments lock progressively: once the year's timetable is published (WF-12), assignment changes always route through the P2 + impact flow (no silent swaps). |
| BR-TCH-009 | Assignments and availability are T2-audited; overrides (qualification, load, gender) T1 with reason. |

## 4. Workflow

Preparation-year assignment building: direct entry by Academic Deputy/HoD within scope (audited), validated live. Post-publication changes and mid-year reassignments: P2 (Deputy) with impact panel (BR-TCH-007/008). Teacher-flag removal: consequence flow of Module 12 offboarding/contract change.

## 5. User roles

Academic Deputy (owner), Head of Department (own-department assignments), Vice Principal (load oversight), HR (teacher flag + availability), Teacher (view own workspace), Timetable owner (consumer), Principal (reports).

## 6. Permissions

| Action | Roles |
|--------|-------|
| View teacher directory/loads | Academic staff (scope) |
| Designate teacher flag | HR |
| Build assignments (prep year) | Academic Deputy; HoD (own department) |
| Change post-publication / mid-year | Deputy + P2 |
| Override qualification/load/gender | Deputy (permission flags, T1) |
| Edit availability | HR, Deputy |
| Teacher workspace | Teacher (own only) |

## 7. Database concept

Entities: `TeacherProfile` (employee ref, teaching flag, specializations display, max-load per year); `TeacherAssignment` (teacher × offering × section, role: primary/co, effective dates); `TeacherAvailability` (year, slot patterns, reason type); load = derived view (assignment periods sum). Homeroom lives in Module 06 (`HomeroomAssignment`) — teacher workspace joins both. Assignment is the FK target for marksheets (17) and the constraint input for sessions (15).

## 8. Required screens

1. Teacher directory — academic view: subjects, load %, homeroom, departments.
2. **Assignment matrix** — per department or per grade: offerings × sections grid with teacher pickers, live load meters per teacher, qualification/gender warnings inline, copy-from-last-year.
3. Load board — all teachers load vs max, under/over highlights, drill to assignments.
4. Availability editor — weekly slot pattern per teacher with reason types.
5. Reassignment wizard — mid-year change with impact panel (marksheets, sessions, notifications preview) + P2 submission.
6. **Teacher workspace (My classes)** — teacher's own hub: today's timetable, my sections/subjects, attendance entry links, marksheet links, my load summary. (The teacher's daily front door — UX priority for Phase 11.)

## 9. Validation rules

Assignment requires active contract + teaching flag; one primary per offering-section (BR-TCH-005); qualification/gender/load checks per strictness config with logged overrides; availability patterns cannot contradict published sessions without triggering the impact flow; effective dates within year; department scope enforced for HoD editing.

## 10. Reports

Teacher load report (by teacher/department: periods vs max, sections count) · Assignment register per grade/section (who teaches what — parent handbook version bilingual) · Qualification-override register (T1 view) · Mid-year reassignment log · Unassigned offerings report (offerings × sections lacking a primary — activation blocker feed for BR-AYR-004) · Availability constraints summary (Module 15 input sheet).

## 11. Dashboard widgets

Academic Deputy: unassigned offerings count (target 0 pre-activation), load outliers (>100%, <60%), pending reassignment approvals. HoD: my department's assignment completeness. Teacher workspace: today's classes, pending marksheets (17 feed), my week at a glance.

## 12. Notifications

`AssignmentPublished` → each teacher (their load sheet); `AssignmentChanged` → affected teacher(s), HoD, Timetable owner (+ parents per BR-TCH-007 config); `LoadExceeded` (override used) → VP; `UnassignedOfferingsRemain` (pre-activation daily) → Deputy; `AvailabilityChanged` → Timetable owner.

## 13. Future enhancements

Substitution pool & auto-suggest (with Module 15 daily cover); teacher appraisal integration (observation schedules against timetable); load-based payroll allowances feed (Module 12 Q3); teacher preference capture (survey-driven soft constraints for scheduling); PD-driven qualification auto-updates.

## 14. Open questions

1. Cross-gender teaching restriction (BR-TCH-003): which target-market configurations are actually required (KG exempt? admin exempt?) — needs pilot-market confirmation; shipped configurable, default off. |
2. Max-load defaults per contract type (24 full-time proposed) — school-configurable; confirm typical values per market. |
3. Should co-teachers see marks (read) or enter drafts pending primary approval? Proposed: read-only + draft-entry configurable per school — confirm in Module 17 detail. |
4. Do HoDs approve assignment changes in their department (extra chain step) or is Deputy P2 enough? Proposed: Deputy only (HoD consulted offline) — confirm. |
