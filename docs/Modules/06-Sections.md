# Module 06 — Sections

**Phase:** 3 — Academic structure | **Status:** Draft for review | **Rule prefix:** `BR-SCN`

---

## 1. Purpose

Manage sections (شعب) — the class groups within a grade for a given academic year — including capacity, gender, homeroom teacher, classroom link, and the student-assignment rules that Attendance, Timetable, marks sheets, and homeroom scoping all depend on.

## 2. Scope

**In:** section creation per grade per year, naming standards, capacity & gender, homeroom teacher assignment, default classroom link, student assignment/transfer between sections, balancing tools, section merge/close mid-year.
**Out:** timetable content (Module 15), teacher subject assignments (Modules 07/13), enrollment itself (Module 03 concept — sections are its target).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-SCN-001 | Sections exist per grade per academic year (year-scoped, BR-GLB-020); naming follows a school pattern (e.g., {GradeCode}-{A,B,C} or bilingual names like خامس-أ); names unique within grade+year. |
| BR-SCN-002 | Section capacity ≤ grade section-size plan (BR-GRD-006) and ≤ linked classroom capacity (Module 08) when a room is linked; assignment beyond capacity requires permission-gated override with reason (T2-audited). |
| BR-SCN-003 | Section gender inherits grade policy and may narrow (BR-GRD-004); student assignment validates gender. |
| BR-SCN-004 | Each section has at most one **homeroom teacher** per year (rich concept: drives "own sections" dynamic scope, doc 06 §4.2, and homeroom notifications); reassignment is effective-dated (history kept — mid-year handover visible). |
| BR-SCN-005 | A student belongs to exactly one section at a time within their enrollment (BR-GLB-024); **section transfer** is effective-dated, reason-coded (balancing, behavioral, parent request, medical), and preserves continuity: attendance/marks stay with the recorded section historically, reports aggregate per student across the transfer. |
| BR-SCN-006 | Section transfer after marks entry has begun triggers a marks-continuity check (Module 17 consumes): open marksheets for the student move to the target section's sheets; published marks never move (they are history). |
| BR-SCN-007 | **Mid-year merge/close**: closing a section requires zero assigned students (transfers first — bulk tool provided); the closed section remains in history; its timetable sessions void forward from the effective date (Module 15 consumes). P2 approval (Vice Principal). |
| BR-SCN-008 | Balancing tools honor configured rules: size balance, gender ratio (mixed sections), language/curriculum grouping, "keep siblings apart/together" preference flags, and "keep-apart" behavioral pairs (Discipline module feed — restricted visibility); rules produce proposals, humans confirm. |
| BR-SCN-009 | Sections are the default scoping unit for teacher permissions, attendance sheets, and parent communications — creation/deactivation propagates to dependent modules through events, never manual re-setup. |

## 4. Workflow

Preparation-year: free creation/editing (audited). Active-year: create/edit direct for empty sections; capacity overrides logged; transfers direct with reason (bulk transfers ≥ configurable count require VP approval P2); merge/close always P2 (BR-SCN-007). Rollover step 5 (BR-AYR §4) is the bulk-assignment entry point.

## 5. User roles

Registrar (owner), Vice Principal (approvals, balancing), Stage Supervisor (own-stage sections), Homeroom Teacher (view own section roster), Sys Admin (structure).

## 6. Permissions

| Action | Roles |
|--------|-------|
| View sections/rosters | Staff per scope (own sections for teachers) |
| Create/edit sections | Registrar, Sys Admin |
| Assign homeroom teacher | Registrar + VP |
| Transfer student (single) | Registrar, Stage Supervisor (scope) |
| Bulk transfer / merge / close | Registrar + VP approval (P2) |
| Capacity override | Registrar (permission flag) — logged |

## 7. Database concept

Entities: `Section` (grade-year profile ref, name bilingual, capacity, gender, default classroom ref, status); `HomeroomAssignment` (section × teacher, effective dates); `SectionMembership` (enrollment × section, effective dates, transfer reason) — effective-dated membership implements BR-SCN-005 cleanly; current section = open-ended membership row. Attendance/marks reference membership-at-date (Phase 10 formalizes).

## 8. Required screens

1. Section list per grade/year — capacity meters, gender, homeroom, room, student counts.
2. Section detail — roster (photos, flags), homeroom history, room link, timetable glance.
3. **Assignment board** — drag-drop students across sections of a grade; rule-based auto-distribute (BR-SCN-008) with proposal diff view; capacity/gender live validation (shared with rollover cockpit).
4. Transfer dialog — single/bulk, effective date, reason code, continuity warnings (BR-SCN-006).
5. Merge/close wizard — target mapping, impact list, P2 submission.

## 9. Validation rules

Name uniqueness per grade+year; capacity/gender checks on every assignment (override path per BR-SCN-002); homeroom teacher must be an active teacher not already homeroom of another section that year (configurable: allow 1, warn at 2); transfer effective date within year and ≥ enrollment start; close blocked with assigned students; auto-distribute proposals never violate hard rules (capacity/gender) even before human confirmation.

## 10. Reports

Section roster (bilingual, printable, with photos) · Section utilization by grade (size vs capacity) · Transfer register (period, reasons — pattern signal for VP) · Homeroom assignment history · Balance report (size/gender distribution across a grade's sections).

## 11. Dashboard widgets

Registrar: sections over/under target size, unassigned students count (must be zero after rollover). VP: transfers this month by reason. Homeroom teacher: my section headcount + today's absentees (Attendance feed).

## 12. Notifications

`StudentTransferred` → parents (new section, homeroom), old & new homeroom teachers; `HomeroomAssigned` → teacher; `SectionOverCapacity` (override used) → VP; `UnassignedStudentsRemain` (post-rollover daily) → Registrar.

## 13. Future enhancements

Subject-level groups (electives/streaming across sections — needed for secondary IGCSE options; flagged for Module 07/15 coordination and likely v1.x); co-teaching (second homeroom); automated keep-apart mining from discipline incident co-occurrence.

## 14. Open questions

1. Subject-option groups (students from multiple sections in one elective class): confirmed out of v1 core but Module 07/15 must not preclude it — architectural note carried to Phase 10.
2. Bulk-transfer approval threshold default (proposed ≥ 5 students) — pilot confirmation.
3. Sibling placement policy default (together/apart/no rule)? Ship as school-configurable preference, no product default.
