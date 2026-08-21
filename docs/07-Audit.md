# 07 — Audit Framework

**Phase:** 2 — Cross-cutting frameworks | **Status:** Draft for review | **Owner:** Security Architect + QA Architect

---

## 1. Purpose

Answer, for any sensitive record at any time: **who** changed **what**, **when**, from **which value to which value**, and **why** — in a way no role can alter, satisfying school owners, external auditors, and data-protection regulators.

## 2. Audit domains

| Domain | Captures | Examples |
|--------|----------|----------|
| **Data audit** | Field-level before/after on classified entities | Mark 78→85; discount 0→20%; guardian phone changed |
| **Security audit** | Authentication & authorization events | Login success/failure, lockout, 2FA change, role grant, scope change, impersonation, export |
| **Process audit** | Workflow steps (from doc 05, BR-WF-002) | Who approved the refund and when, with reason |
| **System audit** | Jobs & operations | Backup run/verify, rollover execution, bulk imports, notification batches |

## 3. Entity classification (tiers)

Each module doc must assign every entity a tier; the framework enforces the tier's behavior.

| Tier | Behavior | Assigned to |
|------|----------|-------------|
| **T1 — Field-level + reason** | Every field change logged with old/new value; reason mandatory on defined fields | Marks after submission, financial documents, discounts/refunds, student identity fields, guardianship links, permissions/roles, certificate data, medical file |
| **T2 — Field-level** | Old/new values logged, reason optional | Student non-identity data, employee files, fee structures, timetable, attendance corrections, admissions data |
| **T3 — Record-level** | Create/modify/deactivate events with actor+time (BR-GLB-007), no field diff | Reference lists, calendars, subjects, classrooms |
| **T0 — Read audit** | View/print/export logging | Restricted categories only (medical, discipline, salary) and all exports (BR-SEC-021) |

## 4. Audit record content

Every entry: entity type + business key (human-readable, e.g., student number — survives even if the row is later deactivated), field, old value, new value (stored bilingual-aware: raw value + display), actor (user + role in effect + delegation flag), UTC timestamp, school, academic year context, source (screen / import / job / API), correlation id (groups one save's changes), reason (where required), client IP.

## 5. Business rules

| ID | Rule |
|----|------|
| BR-AUD-001 | Audit storage is append-only; no update or delete path exists in the application for any role (BR-GLB-081). |
| BR-AUD-002 | Auditing is not optional per school: tiers can be raised by configuration, never lowered below this document's assignments. |
| BR-AUD-003 | A failed business transaction leaves no partial audit entries (atomic with the transaction); a successful one always leaves them (same transaction). |
| BR-AUD-004 | Audit reads are themselves permission-gated (Auditor role or module-scoped audit permission) and T0-logged. |
| BR-AUD-005 | Audit entries display in the viewer's language, but store raw values — a bilingual display layer, single stored truth. |
| BR-AUD-006 | Retention: audit data is retained ≥ 10 years or the country-pack legal minimum, whichever is longer; never purged with operational archival. |
| BR-AUD-007 | Tamper-evidence: periodic integrity checkpoints (e.g., daily hash chaining over the day's entries) so gaps or edits at the storage level are detectable. |
| BR-AUD-008 | Every record's UI shows "Created by/at, Modified by/at" inline; the full history panel is one click for authorized users. |

## 6. Screens

| Screen | Description |
|--------|-------------|
| Record history panel | On any audited record: chronological field changes, workflow steps, reasons — filterable by field |
| Audit explorer | Cross-entity search: by user, date range, entity type, module, action, school, year; export (permission-gated, itself audited) |
| Security event log | Login/lockout/permission-change stream with anomaly highlights (out-of-hours admin actions, repeated failures) |
| Integrity dashboard | Checkpoint verification status, audit volume by module, top changers |

## 7. Reports & widgets

- Sensitive-change register (T1 changes with reasons) per period — Principal/Auditor
- Mark-change report after publication (always includes WF-08 approvals)
- Discount/refund register with approval chains
- Export log (who exported what personal data)
- Dormant account & permission-change reports (with doc 06)
- Widget: "Sensitive changes today" counter on Principal dashboard

## 8. Non-functional notes

Audit writes must not measurably slow interactive saves (async-safe within the transaction boundary per BR-AUD-003); volume estimate and partitioning strategy are a Phase 10 (Database) deliverable; Phase 9 defines which audit reports are standard vs. Auditor-only.

## 9. Future enhancements

Anomaly detection (unusual mark-change patterns before certificate issuance), SIEM export (syslog/JSON feed), legal-hold flags per student file.

## 10. Open questions

1. Country-pack legal retention minima (feeds BR-AUD-006) — pending README Q3 country list confirmation.
2. Is IP/device capture acceptable under target privacy law for portal users, or staff-only? Recommendation: staff always; portal per country pack.
3. Hash-chain checkpoint frequency (daily default) — confirm with pilot customer's audit requirements.
