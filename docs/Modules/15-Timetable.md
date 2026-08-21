# Module 15 — Timetable

**Phase:** 5 — Academic operations | **Status:** Draft for review | **Rule prefix:** `BR-TTB`

---

## 1. Purpose

Build, validate, publish, and operate the weekly teaching schedule — sections × periods × offerings × teachers × rooms — plus the **daily operations layer**: substitutions (cover), room changes, and session-level views feeding period attendance and teacher workspaces.

## 2. Scope

**In:** timetable shape (periods/day, day templates), timetable versions per year/term, manual + assisted construction with hard/soft constraint validation, conflict engine, publication (WF-12), **substitution management** (daily cover), room-change operations, session generation (dated instances from the weekly pattern × calendar), personal timetables (teacher/student/room), portal views.
**Out:** assignment of teachers to subjects (Module 13 — consumed as the allowed set), exam seating timetables (Module 16), auto-generation solver (Future — v1 is assisted-manual, see §13).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-TTB-001 | **Timetable shape** per school/stage: working days (from BR-CAL), periods per day with times (incl. breaks, assembly as non-teaching slots), period durations; shape is year-versioned; shape changes after publication follow the amendment flow (P2 + impact). |
| BR-TTB-002 | A **timetable version** exists per year (optionally per term); exactly one Published version is operational at a time (WF-12: Draft → Validated → Published, P2 VP). Publication requires zero hard-constraint violations. |
| BR-TTB-003 | A **placement** = section × period-slot × offering × teacher(+co) × room. Placement teachers must hold the matching assignment (BR-TCH-002); placement counts must equal the offering's weekly periods (BR-SUB-005) — the completeness check at validation. |
| BR-TTB-004 | **Hard constraints** (block save/publication): teacher double-booking; room double-booking; section double-booking; teacher availability (BR-TCH-006); room maintenance/wing/type requirements at hard strictness (BR-ROM-004/005, BR-ROM-003); gender rules (BR-TCH-003 where hard). |
| BR-TTB-005 | **Soft constraints** (warn + score): teacher max consecutive periods, teacher daily spread/gaps, subject distribution across week (no double Math daily unless flagged block-teaching), heavy subjects in early periods (config), room stability for home-room model (BR-ROM-006), teacher room-travel minimization. Violations listed with a quality score; publication allowed with acknowledgment. |
| BR-TTB-006 | **Sessions** (dated instances) generate from the published pattern × academic calendar (working days only, BR-CAL-003) on publication and rolling forward; calendar amendments (BR-CAL-004) and room maintenance (BR-ROM-004) raise session conflicts into the resolution queue, never silent drops. |
| BR-TTB-007 | **Substitution (daily cover):** for an absent teacher (fed by Module 12 staff attendance/leave), the cover console lists that day's affected sessions; per session assign: qualified substitute (BR-SUB-006 matrix, free at that slot — hard), or merge/supervise options per school policy; substitutions are effective for the dated session only, notified to the substitute, and **counted per teacher for payroll-prep** (Module 12 Q3 accepted: counted export line). |
| BR-TTB-008 | **Room change** (dated, temporary): per session with reason; validates room constraints; visible on all affected views immediately. |
| BR-TTB-009 | Mid-year pattern changes (permanent): new version via amendment flow — history preserved (past sessions stay under the version that generated them); attendance/marks recorded against dated sessions are never rewritten (aligns BR-SCN-005 philosophy). |
| BR-TTB-010 | Timetable data is T2-audited; substitutions/room changes logged with actor+reason; publication events T1. |

## 4. Workflow

Construction (Preparation/Draft): direct grid editing with live validation. WF-12 publication: Timetable owner submits validated version → VP approves → sessions generate, portal/workspaces switch. Amendments: P2 with impact panel. Daily cover: P1 console actions (speed-critical) with full logging — no approval chain (supervisor executes it); optional review report to VP.

## 5. User roles

Timetable Owner (Academic Deputy or dedicated scheduler), VP (publication approval, cover oversight), Supervisors (daily cover execution), Teachers (view own + substitute alerts), HoD (department view), Students/Parents (portal published view), Registrar (reports).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Edit draft versions | Timetable Owner |
| Publish (WF-12) | Owner + VP approval |
| Daily cover / room change | Supervisors, Owner |
| View all timetables | Academic management |
| View own timetable | Teacher (own), Parent/Student (own section, published) |
| Amend published | Owner + P2 |

## 7. Database concept

Entities: `TimetableShape` (stage-year: days, period slots with times); `TimetableVersion` (year/term, status); `Placement` (version × section × slot × offering × teacher × room); `Session` (dated instance: date × placement snapshot, status: held/substituted/room-changed/cancelled); `Substitution` (session × substitute teacher, reason, counted flag); `SessionChangeLog`. Sessions are the FK target for period attendance (Module 14) and the substitution/payroll count source — placement edits never mutate past sessions (snapshot semantics).

## 8. Required screens

1. Shape designer — periods/times per stage with visual day template.
2. **Timetable builder** — section-week grid (primary) + teacher-week and room-week pivots; drag-drop placements; live hard/soft validation panel; completeness meter per section (placed vs required periods); quality score; copy-week/copy-section tools.
3. Conflict & validation board — all violations listed, click-to-locate.
4. Publication console — version diff vs current, checklist, WF-12 submission.
5. **Daily cover console** — today/tomorrow absent teachers → affected sessions → substitute suggestions (qualified + free), one-click assign; cover summary printout for staff room.
6. Session conflict queue — calendar/room-driven conflicts with resolution actions.
7. Personal views — teacher week (workspace embed), section timetable (portal, printable bilingual), room schedule.

## 9. Validation rules

Hard constraints absolute at save (BR-TTB-004); completeness required for publication (every offering fully placed, every section slot accounted — teaching or declared free); substitute must be free + qualified (override: supervise-only mode flagged, not marked qualified-teaching); room change validates capacity/wing/type; version editing locked while under WF-12 review; past-dated sessions immutable (status changes only via cover/cancel flows with reason).

## 10. Reports

Master timetable book (all sections, bilingual, printable) · Teacher timetables (individual sheets) · Room utilization (feeds BR-ROM heatmap) · Free-teacher matrix per period (cover planning) · Substitution register per teacher/period (payroll-prep feed + fairness view) · Curriculum delivery check (placed vs plan per offering) · Session cancellation/change log · Quality score trend per version.

## 11. Dashboard widgets

VP: today's cover status (uncovered sessions = red), substitution load fairness top-5. Timetable owner: unresolved session conflicts, draft completeness. Teacher workspace: today's sessions (with cover flags), my substitutions this month. Portal: child's today/week schedule with live room changes.

## 12. Notifications

`TimetablePublished` → all teachers, parents/students (portal); `SubstitutionAssigned` → substitute (+ original teacher FYI); `SessionRoomChanged` → teacher + section portal; `SessionCancelled` → teacher, parents (config); `UncoveredSessions` (morning deadline) → VP; `AmendmentApproved` → affected staff.

## 13. Future enhancements

**Auto-generation solver** (constraint-programming timetable generator — deliberately Future: v1 ships assisted-manual with full validation; solver is a major engineering line-item and most schools adjust manually anyway); teacher preference capture feeding soft constraints; elective-group scheduling (with BR-SUB-008 / BR-SCN Q1); parent-visible cover notices; digital signage feeds (staff-room screens).

## 14. Open questions

1. Confirm assisted-manual (no solver) is acceptable for v1 sales — competitive GAP risk to assess in Phase 12; mitigation: import from aSc/Untis file formats as an interim (worth scoping?). |
2. Supervise-only cover (non-qualified staff supervising a class): allowed per target-school policy? Shipped as flagged option. |
3. Should Friday/short-day shapes (different period count per day) be supported in v1? **Recommendation: yes** — day-level templates in BR-TTB-001 (already modeled); confirm. |
4. Substitution fairness policy (max covers/week per teacher): report-only (proposed) or hard cap? |
