# Module 29 — Activities

**Phase:** 7 — Student services | **Status:** Draft for review | **Rule prefix:** `BR-ACT`

---

## 1. Purpose

Manage extracurricular life: clubs, sports teams, competitions, trips, and events — with program definitions, consent-gated enrollment, capacity and eligibility rules, schedules that respect the timetable, activity attendance, optional fees, achievements feeding the student file, and trip safety controls.

## 2. Scope

**In:** activity catalog (types: club/sport/competition/trip/event), program instances per term (schedule, venue, supervisor, capacity, eligibility, cost), enrollment with parent consent (trips always), session attendance, trip management (itinerary, transport tie-in Module 23 ad-hoc trips, roster + medical/pickup data pack for supervisors), achievements & participation records (Module 10 tabs feed), activity fees (Module 19 misc/service charge), external competition tracking.
**Out:** inter-school league management (out), coaching performance analytics (Future), facility public rental (out).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-ACT-001 | **Programs:** each activity instance is term-scoped with: bilingual name/description, supervisor(s) (staff), schedule slots (validated against venue availability BR-ROM and — for pull-out programs — timetable conflicts surfaced per policy: after-school free, in-school requires exemption-style approval), capacity, eligibility (stage/grade/gender per BR-GRD-004 policy inheritance), cost (0 or fee), consent-required flag (trips: always). |
| BR-ACT-002 | **Enrollment:** request (portal parent / counter / teacher nomination) → eligibility + capacity check (waitlist per BR-ADM-006 pattern) → consent capture (e-consent portal or signed form doc 10) → fee charge if costed (Module 19) → active. Withdrawal from a program per policy (refund class per BR-FEE-006 category rules). |
| BR-ACT-003 | **Attendance:** per session (roster from enrollment) by supervisor; absence from an in-school-hours activity reconciles with Module 14 (student marked present-at-school but absent-at-activity flags to supervisor); after-school absence notifies parents (config — safeguarding: parents must know a child skipped practice they think the child attends). |
| BR-ACT-004 | **Trips:** extended program type with itinerary (destinations, times), transport plan (Module 23 ad-hoc trip or external), staff ratio rule (config per stage: e.g., 1:10 KG), **roster data pack** for lead supervisor: emergency contacts, medical banner (BR-HLT-002 scope extension for trip duration — explicitly logged access), pickup rules; trip departure checklist (headcount, consents verified, data pack issued) and return headcount confirmation (BR-TRN-005 sweep pattern). |
| BR-ACT-005 | **Consent:** per program (blanket for term clubs) or per trip (always specific); consent records versioned (what was consented, when, by whom); no consent → no participation (hard; no override — product safeguarding stance). |
| BR-ACT-006 | **Achievements:** positions, awards, certificates (Module 18 honor certificates), participation records — write to student file tabs 11/12 (BR-STU-004); competition results tracked per event with external-body references. |
| BR-ACT-007 | Costed programs follow full finance rules (charges/refunds); free programs never generate finance records; supervisor allowances (if paid) are a Module 12 export-line candidate (BR-EXM-005 pattern — config). |
| BR-ACT-008 | Programs/enrollments T2-audited; trip safety events T1; medical data access during trips T0-logged (BR-HLT-001 alignment). |

## 4. Workflow

Program approval: proposer (teacher) → Activities Coordinator → VP (P3; trips + Principal always P4-style elevation). Enrollment: P2-light (coordinator confirms after consent+fee). Trip execution: checklist-gated departure (BR-ACT-004) → return confirmation. Achievement issuance: coordinator → Module 18 chain for certificates.

## 5. User roles

Activities Coordinator (owner), Program Supervisors (teachers/coaches — rosters, attendance, achievements), VP/Principal (approvals, trips), Nurse (trip data pack source — no new rights), Transport Supervisor (trip transport), Parents (portal: browse/enroll/consent/pay-view), Students (browse, self-request per stage config), Finance (charges).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Catalog/program management | Coordinator |
| Approve programs/trips | VP (trips + Principal) |
| Manage own program roster/attendance | Supervisor (program scope) |
| Consent records | Coordinator (view), Parent (grant own) |
| Trip data pack access | Lead supervisor (trip duration, T0) |
| Record achievements | Supervisor → Coordinator confirm |
| Enrollment management | Coordinator |

## 7. Database concept

Entities: `ActivityType`; `Program` (term-scoped, schedule slots, capacity, eligibility, cost ref, consent config); `ProgramEnrollment` (status, consent ref, charge ref, waitlist); `ConsentRecord` (versioned text snapshot); `ActivitySession` + `ActivityAttendance`; `Trip` (extends Program: itinerary, ratio, checklist states, transport ref); `Achievement` (student, program/event, type, certificate ref); `CompetitionEvent`. Venue slots reference Module 08; charges reference Module 19. |

## 8. Required screens

1. Program catalog & builder — schedule picker (venue/timetable conflict surfacing), eligibility, cost, consent template.
2. Enrollment board — per program: roster, waitlist, consent/fee status columns.
3. Supervisor view — my programs: session attendance quick-mark, roster with flags, achievements entry.
4. **Trip console** — itinerary, staff assignment vs ratio meter, consent tracker, data-pack generation (T0-logged), departure checklist, return confirmation.
5. Achievements center — records, certificate batch (Module 18), honor board feed.
6. Portal: activities browser (per child eligibility), enroll + e-consent + fee view, schedule in family calendar, achievements showcase.

## 9. Validation rules

Capacity/eligibility hard checks (waitlist path); schedule slots must not clash with the student's timetable for in-school programs (policy-gated); trips: ratio satisfied, all consents specific and current, transport plan confirmed before departure-checklist completion; consent text version bound to enrollment; achievements require program/event linkage; costed enrollment blocked until charge posted (payment per school policy — charge-first default). |

## 10. Reports

Program catalog & participation per term (by grade/gender — inclusion metrics) · Enrollment vs capacity per program · Activity attendance summaries · Trip register (with checklist compliance — safety audit) · Consent audit report · Achievements & competition results register · Activity fee revenue (finance tie-in) · Participation-per-student distribution (students with zero activities — engagement flag). |

## 11. Dashboard widgets

Coordinator: active programs, pending approvals/consents, trips upcoming (checklist status). VP: trip approvals pending, participation rate. Supervisor: my next sessions, my roster alerts. Portal: child's activities this week, new programs open for enrollment.

## 12. Notifications

`ProgramOpenForEnrollment` (eligible families) → parents; `EnrollmentConfirmed/Waitlisted` → parent; `ConsentRequired/Expiring` → parent; `SessionCancelled/Changed` → program parents; `TripReminder` (D-1: logistics) → parents; `TripDeparted/Returned` (headcount confirmed) → parents (safety class); `ActivityAbsence` (after-school) → parents; `AchievementRecorded` → parents (celebrate). |

## 13. Future enhancements

Inter-school competition/league coordination (multi-school future); coach performance & athlete development tracking; equipment checkout (Library circulation pattern reuse); activity marketplace with external providers (vetting workflow); points/house system gamification (with Module 25 merits).

## 14. Open questions

1. In-school-hours pull-out activities (missing class for training): allowed per target schools? Policy-gated design ships; confirm default off. |
2. Trip staff-ratio defaults per stage (1:10 KG / 1:15 primary / 1:20 secondary proposed) — confirm per pack/insurance norms. |
3. Student self-enrollment (no parent) for upper grades in free clubs — proposed allowed with parent FYI notification; confirm. |
4. House/points system (inter-house competition culture in British-curriculum schools): v1 or Future? Proposed Future unless a pilot demands it. |
