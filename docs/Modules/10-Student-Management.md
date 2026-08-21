# Module 10 — Student Management

**Phase:** 4 — People | **Status:** Draft for review | **Rule prefix:** `BR-STU`

---

## 1. Purpose

The **complete electronic student file** — the permanent, multi-year, single source of truth for every student (BO-02): identity, family, medical, transport, attendance, fees, documents, academic history, achievements, activities, behavior — aggregated in one place with audit and attachments, surviving from admission to graduation and beyond.

## 2. Scope

**In:** student master record and statuses, the aggregated student file (16 tabs below), guardians & emergency contacts, per-student subject exemptions (adopted from Module 07 Q3), student lists/search, withdrawal workflow (WF-03) & transfer certificates trigger, returning-student re-admission, ID cards, data-protection lifecycle.
**Out (owned elsewhere, surfaced here read-through):** attendance capture (14), marks (16/17), fee posting (19–21), medical data entry (24), discipline entry (25), transport assignment (23), activities (29). This module owns the **aggregation view** and the student master; owning modules keep their entry screens and rules.

## 3. Business rules

| ID | Rule |
|----|------|
| BR-STU-001 | One student = one permanent record + number (BR-GLB-002, BR-NUM-004), across years, withdrawal, and re-admission. Identity fields (names Ar/En, DOB, gender, nationality, IDs) are T1-audited with reason. |
| BR-STU-002 | Student statuses: `Enrolled → (Suspended) → Withdrawn / Graduated / Transferred-out`; plus `Alumni` (post-graduation). Status changes only via workflows (enrollment via Modules 09/03; withdrawal via WF-03; graduation via rollover). Suspended (disciplinary/financial per school policy) blocks portal result visibility per configuration, never blocks parent fee visibility. |
| BR-STU-003 | Every student links ≥ 1 parent (Module 11) with a **relationship type** and flags: primary contact, financially responsible (≥ 1 required), pickup-authorized, portal visibility (custody revocation per BR-SEC-011). **Guardians** (non-parent legal custody) attach with mandatory court/authorization document (🔒). **Emergency contacts** (≥ 1 required beyond parents) carry name, relation, phones, pickup-authorized flag. |
| BR-STU-004 | The **student file** presents 16 tabs (§8) aggregating owning modules read-through with per-tab permission gating (medical/discipline/finance follow BR-GLB-072 — a user sees only tabs they're entitled to). |
| BR-STU-005 | **Subject exemptions**: per student per offering (e.g., Islamic studies, swimming-medical) with reason type, approval (P2 — Registrar + Principal for curriculum subjects), and effective dates; Attendance treats exempted periods as excused-by-exemption; Grading excludes the offering from the student's GPA per Module 17 rules. |
| BR-STU-006 | **Withdrawal (WF-03)**: request (parent or school-initiated) → parallel clearance checklist (Finance settle-or-plan, Library returns, Store/Cafeteria balances, Transport deactivation) with **finance veto** → Registrar completes → status Withdrawn, portal narrowed to financial+documents, transfer certificate issuable (Module 18 executes issuance, TC number per doc 08). Mid-year fee treatment per Module 19 refund/pro-ration policy. |
| BR-STU-007 | Re-admission of a returning student reactivates the original record through the Admissions pipeline (BR-ADM-007), preserving all history; the file shows the enrollment gap explicitly. |
| BR-STU-008 | Photos: consent-governed (country pack, doc 10 Q1); shown on rosters, ID cards, attendance sheets per permission; parents update requests via portal → Registrar approval. |
| BR-STU-009 | Data protection: subject-access export (full-file PDF for a guardian request, permission-gated, T0-audited); retention after final departure per country pack; legal-hold flag suspends purge. |
| BR-STU-010 | Bulk student data changes (import, mass field update) are restricted to Sys Admin with mandatory dry-run preview and are T1-audited per affected record. |

## 4. Workflow

Enrollment/graduation flows owned by Modules 09/03. Owned here: **WF-03 withdrawal** (BR-STU-006, parallel clearance + finance veto — doc 05 Q1 resolved: parallel checklist), **subject exemption** (P2), **student-initiated status changes** (suspension per Discipline module decision feed). Identity-field corrections: direct edit with reason (T1) — no approval chain, audit suffices (recommendation; see Q3).

## 5. User roles

Registrar (owner), Admissions (creation via pipeline), Homeroom Teacher (own-section view, limited tabs), Stage Supervisor (scope view), Nurse (medical tab), Finance (finance tab), Principal (full view), Parent/Student (portal self-view), Auditor (read + audit).

## 6. Permissions

| Tab/Action | Default roles |
|-----------|---------------|
| Identity & family view | Registrar, Principal, Homeroom (own), Supervisor (scope) |
| Identity edit | Registrar (T1) |
| Medical tab | Nurse, Principal (+ emergency banner to teachers per BR-GLB-072) |
| Behavior tab | Discipline roles, Principal, Homeroom (own) |
| Finance tab | Finance roles, Principal |
| Exemptions | Registrar propose, Principal approve |
| Withdrawal workflow | Registrar + clearance roles |
| Full-file export | Registrar + Export permission (T0) |
| Portal self-view | Parent (own children), Student (own, published data) |

## 7. Database concept

Entities: `Student` (person identity, permanent number, photo ref, status); `StudentGuardianLink` (student × parent/guardian, relationship, flags, effective dates, custody restriction); `EmergencyContact`; `SubjectExemption` (student × offering, type, approval, dates); `WithdrawalCase` (clearance items, veto states). The file's other tabs are **views over owning modules' data** keyed by student — no duplication. Enrollment/SectionMembership from Module 03/06 provide the year dimension. Academic history = enrollments + published results (17) + external records (BR-ADM-009).

## 8. Required screens — the Student File (tabbed)

1 Personal (identity Ar/En, IDs, photo) · 2 Parents & Guardians (links, flags, custody 🔒) · 3 Emergency contacts · 4 Medical 🔒 (read-through Module 24 + emergency banner) · 5 Transportation (route/stop/fees read-through 23) · 6 Attendance (year summaries, patterns, read-through 14) · 7 Fees (statement, aging, plans read-through 19–21) · 8 Documents (checklist + attachments, doc 10) · 9 Certificates (issued register read-through 18) · 10 Academic history (years, grades, results, external records) · 11 Achievements (honors, awards) · 12 Activities (participation read-through 29) · 13 Behavior 🔒 (incidents/merits read-through 25) · 14 Exemptions · 15 Audit (record history panel, doc 07) · 16 Notes (typed, permission-scoped).

Plus: student search/list (global, filters, saved views, export-gated), withdrawal wizard (clearance board), ID card print (batch per section, template with photo/number/QR), portal student profile (parent/student view of permitted tabs).

## 9. Validation rules

Bilingual names mandatory; DOB sane vs grade age rule; ≥ 1 financially-responsible parent link; ≥ 1 emergency contact; ID formats per country pack with expiry tracking (BR-ATT-008); photo format/size per BR-ATT rules; exemption dates within year; withdrawal blocked while clearance red (finance veto absolute, override only Principal + reason T1); status transitions only via defined flows (BR-WF-001).

## 10. Reports

Student profile sheet (full file print, bilingual, section-gated) · Students register by grade/section/status · Family composition report (siblings in school) · New/withdrawn students per period · Guardianship & custody exceptions 🔒 · Exemptions register · Missing data quality report (no photo, expired ID, no emergency contact) · Data-retention due list 🔒 · Statistical summaries (nationality, gender, age distributions — ministry formats per pack).

## 11. Dashboard widgets

Registrar: data-quality counters (missing docs/photos/contacts), withdrawals in progress, exemption approvals pending. Principal: enrollment by status/stage trend, withdrawal reasons this term. Homeroom: my section birthdays, alerts (medical banner, custody flags).

## 12. Notifications

`StudentRegistered` → parent (Module 09 hands off); `WithdrawalStarted/Cleared/Completed` → parent, clearance roles, finance; `ExemptionDecided` → parent, affected subject teachers; `IDExpiring` → Registrar, parent; `DataRetentionDue` → Registrar 🔒; `PhotoChangeRequested` → Registrar.

## 13. Future enhancements

Alumni module (contact upkeep, transcript requests); sibling-view family dashboard; predictive at-risk flags (attendance+behavior+fees composite); ministry e-integration (Noor-style sync) per country pack; student self-service data-update requests.

## 14. Open questions

1. Is `Suspended` a real status in target schools (vs discipline action without status change)? Modeled as optional status via Discipline feed — confirm. |
2. Full-file export content: does it include audit tab? Recommendation: no — audit available separately to Auditor. |
3. Identity-correction flow: direct-edit-with-audit (proposed) vs approval workflow? Approval adds friction for typo fixes; audit + T1 reason deemed sufficient — confirm. |
4. QR on ID cards linking to a verification endpoint (no personal data in QR) — include in v1? Recommendation: yes, cheap and useful (gate/transport scanning future). |
