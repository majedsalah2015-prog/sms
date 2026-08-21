# Module 04 — Academic Calendar

**Phase:** 3 — Academic structure | **Status:** Draft for review | **Rule prefix:** `BR-CAL`

---

## 1. Purpose

Define, per school per academic year, which days are working days, holidays, and events — the authoritative day-type source consumed by Attendance, Timetable, Examinations, and Fees (due-date shifting), per BR-GLB-052.

## 2. Scope

**In:** day-type calendar (working/weekend/holiday/event/exam period), event catalog (bilingual, categorized, audience-targeted), semester/term boundary visualization, Hijri overlay, portal publication, mid-year change control with impact analysis.
**Out:** timetable periods (Module 15), exam schedules (Module 16 — they *reserve* calendar exam periods), personal/staff leave (Module 12).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-CAL-001 | Every date in an academic year resolves to exactly one day type: Working / Weekend / Holiday / Partial (short day) / Exam-period working day. Weekend days derive from school working-week config (BR-GLB-012) and can be overridden per date (make-up working Saturday). |
| BR-CAL-002 | Holidays and events are bilingual, categorized (national, religious, school event, professional day), and may target audiences (all, students-only — e.g., staff training day where staff attend). |
| BR-CAL-003 | Attendance can only be recorded on days that are working for the audience in question (students vs staff — BR-GLB-052); Timetable generates sessions only on working days; fee due dates falling on non-working days shift per policy (next working day default — Module 20 consumes). |
| BR-CAL-004 | **Mid-year changes to past dates are blocked**; changes to future dates that already carry data (recorded attendance on a to-become-holiday date, scheduled exams) require impact review: the screen lists affected records and the resolution action (void attendance, reschedule exams) before saving — P2 approval (Vice Principal). |
| BR-CAL-005 | Hijri display overlays per school config (ADR-4); religious holidays whose Hijri dates map to uncertain Gregorian dates can be entered provisionally and confirmed later (flagged until confirmed). |
| BR-CAL-006 | Minimum instructional days: the calendar shows a live count of working days per semester/term against a configurable ministry minimum; activation warning (not block) when below. |
| BR-CAL-007 | The calendar is versioned: publication snapshots a version to the portal; subsequent edits require re-publication (parents see only published versions). |
| BR-CAL-008 | Calendar is T2-audited; mid-year change approvals carry mandatory reason. |

## 4. Workflow

Draft (Preparation year) → Published (with year activation, or explicitly) → Amendments via BR-CAL-004 impact-review flow (P2) → Re-published. Event creation is direct entry (audited); only day-type changes affecting existing data need approval.

## 5. User roles

Registrar / Sys Admin (author), Vice Principal (amendment approver), Principal (view/report), all staff + portal (consume published).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Edit draft calendar | Registrar, Sys Admin |
| Publish / re-publish | Registrar + VP approval when amendments touch existing data |
| Approve impact changes | Vice Principal |
| View published | All authenticated (staff + portal) |

## 7. Database concept

Entities: `CalendarDay` (year × date → day type, audience override, source: rule/manual); `CalendarEvent` (bilingual, category, date range, audience, portal-visible flag); `CalendarVersion` (publication snapshots). Day types materialized per date (fast joins for attendance/timetable) while weekend rules stay derivable from config. Consumers reference CalendarDay, never re-implement week logic.

## 8. Required screens

1. **Year calendar board** — month grid + full-year heat view, day-type painting (drag ranges), Hijri overlay toggle, working-day counters per term (BR-CAL-006 live).
2. Event manager — list + calendar placement, bilingual entry, audience targeting, portal visibility.
3. Amendment impact review — affected-records list (attendance, exams, timetable sessions) with resolution picker, approval submission.
4. Portal calendar view — published events + holidays, per family audience, print/PDF, iCal export.

## 9. Validation rules

Dates within the academic year only (BR-GLB-051); no unresolved day (defaults from working-week rule); event ranges valid; provisional Hijri holidays flagged until confirmed (BR-CAL-005); publication blocked while impact reviews pending; instructional-day warning surfaced at publication.

## 10. Reports

Official year calendar (bilingual, printable — parents/ministry) · Working days per term vs minimum · Calendar amendments register (what changed after publication, approvals) · Holiday list by category.

## 11. Dashboard widgets

All-staff shell: today's day type + upcoming events strip. Principal: instructional-day counter vs minimum. Portal home: next holiday/event.

## 12. Notifications

`CalendarPublished` / `CalendarAmended` → staff + parents (portal/email digest); `EventReminder` (D-1, portal-visible events) → targeted audience; `ProvisionalHolidayUnconfirmed` (D-14 before provisional date) → Registrar.

## 13. Future enhancements

Multi-school group calendar with per-school overrides; RSVP-able events (with Activities module); weather/emergency same-day closure broadcast flow (ties Notifications urgent class + attendance auto-void).

## 14. Open questions

1. Ministry minimum instructional days per country pack — values needed (BR-CAL-006).
2. Are staff working days on student holidays common enough to need audience-split day types in v1 (assumed yes — BR-CAL-001/002 support it); confirm with pilot.
3. Same-day emergency closure (snow day equivalent): v1 manual flow (amend + notify) or dedicated one-click flow? Recommendation: dedicated flow in v1.1, manual in v1.
