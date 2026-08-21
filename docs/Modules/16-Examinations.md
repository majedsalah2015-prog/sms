# Module 16 — Examinations

**Phase:** 5 — Academic operations | **Status:** Draft for review | **Rule prefix:** `BR-EXM`

---

## 1. Purpose

Plan and run formal assessments: exam types and blueprints per term, conflict-free exam schedules, seating & invigilation, marks capture against defined maximums, exam-day attendance with makeup handling — handing clean raw marks to Module 17 (which owns calculation, scales, GPA, ranking, and result documents).

## 2. Scope

**In:** exam type catalog, assessment blueprint per term (which components exist per offering, max marks, weights — jointly owned with Module 17, see BR-EXM-002), exam schedules (dated, within calendar exam periods), room/seating allocation (exam capacity, BR-ROM-002), invigilation duty roster, exam-session attendance & incident log (cheating cases → Discipline), makeup/re-sit exams, marks capture workflow start (entry screens; approval chain formalized in Module 17 WF-07).
**Out:** grading scales, GPA, ranking, report cards, transcripts (Module 17); certificates (Module 18); continuous-assessment capture (classwork/homework marks — Module 17 owns entry via the same marksheet mechanism).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-EXM-001 | **Exam types** are school-configurable (quiz, midterm, final, practical, oral, makeup…) with flags: scheduled (needs timetable slot) vs classroom-level; counts-toward-term weight (consumed by Module 17 blueprints); makeup-eligible. |
| BR-EXM-002 | The **assessment blueprint** per offering per term (components: e.g., Quiz 10 + Midterm 20 + Coursework 20 + Final 50) is defined once, owned jointly: Module 16 defines *exam components* existence/max/date-linkage; Module 17 owns *weights, calculation, and locking*. Blueprint locks when the term's first marks post (changes then = P2 Principal + recalculation impact display). |
| BR-EXM-003 | **Exam schedules** exist per exam round (e.g., Final Exams Term 1): dated exams per grade × offering within calendar exam periods (BR-CAL-001); validation: one exam per section per day (config max 2 for upper grades), no schedule outside working days, gap rules between heavy subjects (soft), room exam-capacity totals sufficient (BR-ROM-002). Published schedules notify parents/students (BR-NOT catalog). |
| BR-EXM-004 | **Seating & rooms:** exam sittings allocate students (possibly mixed sections, alphabetic/serpentine distribution per config) to rooms against exam capacity; seating lists printable per room; wing/gender rules honored (BR-ROM-003). |
| BR-EXM-005 | **Invigilation:** duty roster per sitting from available staff (free per timetable, fairness counters); swaps permission-logged; duty sheets printable; duty counts exportable (payroll-prep line candidate, Module 12 pattern). |
| BR-EXM-006 | **Exam-day attendance** per sitting: present/absent/late; absence classifies per Module 14 taxonomy (excused/medical/unexcused) via the justification lifecycle; **excused absence triggers makeup eligibility** per policy; unexcused = zero or policy-defined treatment (Module 17 consumes the classification, never auto-zeroes without policy flag). |
| BR-EXM-007 | **Incidents** (cheating, disruption): logged per sitting with evidence (doc 10), immediate invigilator report → academic decision (mark treatment per policy via Module 17) and disciplinary path (Module 25 case opened automatically for defined categories). |
| BR-EXM-008 | **Makeup exams:** scheduled rounds for eligible students; makeup marks replace or cap the original component per policy (config: full value / capped at pass — Module 17 applies); eligibility list is system-derived (excused absences + approved appeals), manually extendable with permission (T1). |
| BR-EXM-009 | **Marks capture** happens per marksheet (offering × section × component): entry by the assigned teacher (BR-TCH-002 rights), against component max; double-entry verification mode configurable for finals (second entrant, mismatch report); sheets then enter WF-07 (submit → HoD → publish) — chain detailed in Module 17. |
| BR-EXM-010 | Exam papers/model answers may attach per exam (doc 10, restricted until exam date — timed visibility); post-exam they become review material per school policy. |
| BR-EXM-011 | Schedules/rosters T2-audited; marks T1 from first entry (doc 07); incident records 🔒 restricted. |

## 4. Workflow

Exam round: `Draft schedule → Validated → Published (P2 VP)` (mirrors WF-12 pattern). Sitting operations day-of: attendance + incidents (P1 logged). Makeup round: eligibility auto-list → confirm → mini-schedule (P2). Marks: entry (assigned teacher) → verification (config) → WF-07 chain (Module 17). Incident academic decisions: P3 (HoD → Principal) 🔒.

## 5. User roles

Exams Officer (owner), VP (publication, incidents), HoD (department oversight, WF-07 step), Teachers (entry for own marksheets, invigilation duties), Invigilators (attendance + incident logging), Registrar (eligibility, reports), Students/Parents (portal: schedules, seating info per policy).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Configure exam types/blueprints | Exams Officer + Module 17 owner roles |
| Build/publish schedules | Exams Officer + VP (P2) |
| Seating & invigilation rosters | Exams Officer |
| Sitting attendance/incidents | Invigilators (sitting-scoped) |
| Marks entry | Assigned teacher (marksheet-scoped) |
| Extend makeup eligibility | Exams Officer (T1) |
| Incident decisions 🔒 | HoD, VP, Principal |

## 7. Database concept

Entities: `ExamType`; `ExamRound` (term, status); `Exam` (round × offering × grade, date/time, duration, component ref); `ExamSitting` (exam × room, seat allocations); `InvigilationDuty` (sitting × staff); `ExamAttendance` (student × sitting); `ExamIncident` 🔒; `MakeupEligibility`; `Marksheet` + `MarkEntry` (shared structure with Module 17 — single marks store, components flagged exam vs coursework; **one marks model, two modules feeding it** — Phase 10 will formalize as one aggregate). |

## 8. Required screens

1. Exam type & round setup.
2. **Schedule builder** — grid per round (grades × days), drag exams, validation panel (calendar, clash, gap, capacity), publish console.
3. Seating allocator — sittings per exam, room fill with distribution rules, printable room lists + door cards.
4. Invigilation roster — duty grid with fairness counters, swap flow, printable duty sheets.
5. Sitting console (invigilator) — attendance quick-mark, incident capture with photo/attachment.
6. Makeup manager — eligibility list, round scheduling, replacement policy display.
7. **Marks entry sheet** — marksheet grid (students × component), max-bound inputs, absent auto-flag from sitting attendance, save-progress, submit-to-WF-07; double-entry mode UI.
8. Portal: exam schedule per child, seat/room info (config), makeup notices.

## 9. Validation rules

Exams within exam-period working days; clash rules per BR-EXM-003; sitting capacity ≤ room exam capacity; every scheduled student seated exactly once; invigilator free at slot; marks ≤ component max, numeric per component type (Module 17 mark-types); absent students blocked from mark entry (status drives treatment); double-entry mismatches must clear before submission; incident records require category + narrative. |

## 10. Reports

Exam schedule (bilingual, per grade — parent format) · Seating charts per room · Invigilation duty roster + fairness/duty-count export · Exam attendance summary + absentee list per sitting · Incident register 🔒 · Makeup eligibility & results register · Marks-entry progress (sheets entered/submitted per round — the crunch-week monitor) · Blueprint coverage check (components with no marks). |

## 11. Dashboard widgets

Exams Officer: round countdown, entry progress %, unseated students, unfilled duties. VP: incidents today 🔒, publication pendings. Teacher workspace: my marksheets due (entry deadlines), my invigilation duties. Portal: next exam per child.

## 12. Notifications

`ExamSchedulePublished` → parents, students, teachers; `ExamReminder` (D-1) → parents/students; `InvigilationAssigned/Changed` → staff; `ExamAbsenceRecorded` → parents (same day); `MakeupScheduled` → eligible families; `MarksEntryDeadline` (D-2, overdue) → teachers, HoD; `IncidentRecorded` 🔒 → VP (+ parents per policy/Module 25 flow). |

## 13. Future enhancements

Online exam delivery (LMS line); OMR bubble-sheet scanning for MCQ marks; question banks & paper generation; seating auto-optimization (constraint solver shared with Module 15 Future); anomaly detection on mark patterns (with doc 07 Future).

## 14. Open questions

1. Unexcused exam absence policy default: zero vs "denied - counts as attempted"? Ship as policy config; confirm market norm (proposed default: zero with makeup denied). |
2. Double-entry verification for finals: v1 config (proposed) or Future? Cheap given marksheet model — keep v1; confirm. |
3. Seat/room info on portal: shown (reduces exam-day chaos) or hidden (integrity)? Proposed: shown room, not seat. |
4. Invigilation duty allowances: counted export line like substitutions (BR-EXM-005)? Recommend yes — confirm with Module 12 export layout. |
