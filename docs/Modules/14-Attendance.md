# Module 14 — Attendance

**Phase:** 5 — Academic operations | **Status:** Draft for review | **Rule prefix:** `BR-ATD`

---

## 1. Purpose

Capture and manage student attendance — daily and per-period — with the full status taxonomy (late, early leave, permission, medical, excused, unexcused), escalation rules, and same-day parent notification, in ≤ 2 minutes per section per capture (BO success metric).

## 2. Scope

**In:** daily attendance mode, period attendance mode (per school/stage config), status taxonomy & justification lifecycle, late/early-leave gate events, leave-pass (permission) requests, excuse submission & review, correction control (WF-14), escalation thresholds, attendance analytics; staff attendance is **out** (Module 12 owns it).
**Out:** transport boarding attendance (Module 23), activity attendance (Module 29), exemption semantics (Module 10 feeds), timetable sessions (Module 15 provides the period skeleton).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-ATD-001 | Mode per stage (school config): **Daily** (one status/student/day — typical KG/primary) or **Period** (status per timetable session — typical secondary). Period mode derives a computed daily summary per configured formula (e.g., absent ≥ N periods = absent day). |
| BR-ATD-002 | Status taxonomy (product-fixed core, school-extensible labels): `Present · Late · Absent-Excused · Absent-Unexcused · Medical Leave · Permission (leave pass) · Early Leave · Exempted` (from BR-STU-005). Absences start **Unexcused** until a justification is accepted (BR-ATD-005). |
| BR-ATD-003 | Attendance records exist only for enrolled students on audience-working days (BR-CAL-003), against their section-membership-at-date (BR-SCN-005); capture by homeroom teacher (daily mode) or session teacher (period mode, from the session screen) within their scope; Registrar/supervisors can capture for any scoped section. |
| BR-ATD-004 | **Late**: arrival after cutoff (config per stage) — gate entry (reception logs arrival time) or teacher mark; accumulating lates convert per policy (e.g., 3 lates = 1 unexcused absence — configurable, computed not manual, shown transparently). **Early leave**: requires an authorized-pickup person (BR-PAR-008 flags checked at the gate screen) + reason; logged with time and releaser. |
| BR-ATD-005 | **Justification lifecycle:** parent submits excuse (portal upload or paper at counter) within N days (config, default 3 working days) → reviewer (homeroom/supervisor per config) accepts → status flips to Excused/Medical (medical requires document, doc 10). Rejections keep Unexcused with reason. All flips T2-audited. |
| BR-ATD-006 | **Permission (leave pass):** in-day short leave request (parent or staff-initiated) → P2 approval (supervisor) → gate release; returns logged. Distinct from early leave (no return). |
| BR-ATD-007 | **Capture closure:** attendance for a day locks at day-end job (config time); post-closure corrections require WF-14 (P2 + reason). Uncaptured sections at a mid-morning deadline escalate to the supervisor (data completeness is enforced, not hoped for). |
| BR-ATD-008 | **Escalation thresholds** (configurable): same-day absence → parent notification (BR-NOT catalog); consecutive absences ≥ N → homeroom + supervisor task; cumulative unexcused ≥ X% → formal warning letter workflow (with Discipline module linkage per policy); ministry truancy reporting thresholds per country pack. |
| BR-ATD-009 | Attendance % calculations (per student/section/subject) are defined centrally: base = scheduled working days (or periods) minus exempted; consumers (report cards Module 17, certificates, ministry reports) use this single computation. |
| BR-ATD-010 | Exam-period days count as working days (BR-CAL-001); exam-session attendance is captured by Module 16 (absence there triggers makeup rules) and reflected here. |

## 4. Workflow

Capture (P1 direct, scope-gated) → day closure (job) → corrections via WF-14. Justification: `Submitted → Accepted/Rejected` (P2). Leave pass: `Requested → Approved → Released → Returned` (P2). Warning-letter escalation: threshold event → Registrar/supervisor confirms → letter generated (numbered, Module 18 pattern) → parent notified.

## 5. User roles

Homeroom Teacher / Subject Teacher (capture within scope), Attendance Supervisor / Stage Supervisor (monitor, review excuses, approve passes), Receptionist (gate: late arrivals, releases), Registrar (corrections, reports), Principal/VP (analytics), Parent (portal: view + submit excuses + request passes), Student (view own).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Capture (own scope) | Teachers (session/homeroom), Supervisors |
| Gate events (late/release) | Receptionist |
| Review justifications | Homeroom or Supervisor (config) |
| Approve leave passes | Supervisor |
| Post-closure correction | Supervisor + P2 (WF-14) |
| Analytics/exports | Registrar, VP, Principal (export-gated) |
| Portal submit/view | Parent (own children) |

## 7. Database concept

Entities: `AttendanceDay` (student × date: status, computed flags, source) and/or `AttendancePeriod` (student × session: status) per mode; `GateEvent` (arrival/release times, releaser, pickup person); `Justification` (absence refs, type, documents, review state); `LeavePass` (workflow-managed); `EscalationCase` (threshold hits, actions). Daily summaries in period mode are computed-persisted (reporting speed). All records reference enrollment + membership-at-date; capture screens are generated from Module 15 sessions in period mode.

## 8. Required screens

1. **Section capture sheet** — roster with photos, one-tap statuses, default-all-present, offline-tolerant save (single POST), ≤ 2-min target; period variant embedded in teacher session view (Module 15/13 workspace).
2. Gate console — late arrivals (search student → time-stamped Late), early releases (pickup-person verification against authorized list), leave-pass release/return.
3. Attendance monitor — supervisor live board: capture completeness by section (red = uncaptured), today's absences/lates, threshold alerts.
4. Justification review queue — excuse cards with documents, accept/reject with reason.
5. Correction screen — post-closure WF-14 flow.
6. Analytics — student/section/grade trends, day-of-week patterns, repeat-absentee list.
7. Portal: child attendance calendar, excuse submission, leave-pass request.

## 9. Validation rules

Capture only for working days/sessions and enrolled-in-section students; no duplicate records (one per student-day or student-session); early-leave requires authorized pickup match or override (T1); justification window enforced (late submissions require supervisor permission); medical status requires document; correction reasons mandatory; closure job tolerates stragglers via escalation, never auto-fills statuses.

## 10. Reports

Daily absence report (per school/stage — the 9 a.m. report) · Section register (monthly grid, bilingual, ministry format) · Repeat absentees / chronic (≥ X%) list · Lateness patterns (by student, by weekday) · Justification outcomes register · Leave-pass register · Attendance % by student for report cards (Module 17 feed) · Truancy/ministry statutory formats per pack · Correction register (WF-14 audit view).

## 11. Dashboard widgets

Principal: today's attendance % (school, by stage), chronic-absentee count. Supervisor: uncaptured sections (live), pending excuses/passes. Homeroom: my section today + week trend. Portal (parent): child's month at a glance.

## 12. Notifications

`StudentAbsent` (same-day, post-capture-deadline batch) → parents; `LateArrival` → parents (config); `RepeatedAbsence` (threshold) → parents + homeroom + supervisor; `JustificationDecided` → parent; `LeavePassApproved/Released` → parent (release = safety class, bypasses quiet hours per BR-NOT-004); `SectionUncaptured` (deadline) → teacher + supervisor; `WarningLetterIssued` → parent (formal).

## 13. Future enhancements

RFID/biometric gate integration (interface per BR-EMP-005 pattern); bus-to-gate arrival reconciliation (Module 23 link); auto-SMS on gate scan; predictive absenteeism flags; parent pre-notification of planned absence (calendar-based).

## 14. Open questions

1. Daily-summary formula in period mode (absent ≥ N periods = absent day): default N? Proposed: majority of periods; per-school config — confirm typical policy. |
2. Late-to-absence conversion (3 lates = 1 absence): confirm this is policy in target markets or ship disabled by default. **Recommendation: disabled by default** (transparency concerns), enable per school. |
3. Should teachers see justification documents (medical 🔒)? Proposed: reviewers only; teachers see resulting status. Confirm. |
4. Ministry truancy thresholds/formats per country pack — values needed. |
