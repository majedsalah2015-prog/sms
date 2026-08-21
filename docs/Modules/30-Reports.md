# Module 30 — Reports (Reporting Platform)

**Phase:** 8 — Platform | **Status:** Draft for review | **Rule prefix:** `BR-RPT`

> This module defines the reporting **platform**. The full report **catalog** (150+ reports classified by module) is the Phase 9 deliverable in `docs/Reports/`.

---

## 1. Purpose

One reporting engine serving every module: parameterized, permission- and scope-filtered, bilingual/RTL-perfect output (screen, PDF, Excel), with scheduling, favorites, and statutory formats per country pack — so every report listed in module docs runs through one consistent pipeline.

## 2. Scope

**In:** report registry (every catalog report is a registered definition), parameter framework (year/grade/section/date-range pickers standard), execution engine (interactive + queued async for heavy reports per NF-P5), output formats (HTML print view, PDF, XLSX; CSV for data exports), scheduling & subscriptions (email/portal delivery), favorites & recent, statutory pack reports, report-level permissions.
**Out:** ad-hoc query builder / BI self-service (Future), dashboards (Module 31), the catalog content itself (Phase 9).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-RPT-001 | Every report is a registered definition: code (RPT-<MOD>-###, matching the Phase 9 catalog), bilingual title, owning module, parameter set, output formats, permission requirement, sensitivity class (normal / personal-data / restricted 🔒). |
| BR-RPT-002 | **Scope enforcement:** report data is filtered by the runner's data scopes (doc 06) at the query layer — a homeroom teacher running "Section Register" sees only their sections; no report bypasses scoping (BR-GLB-071). |
| BR-RPT-003 | Personal-data and restricted reports require Export permission for file output and are T0-audited with parameters + row counts (BR-SEC-021); restricted reports never schedule to email (portal-delivery only, config). |
| BR-RPT-004 | **Bilingual output:** every report renders in the runner's language (or an explicit language parameter for official documents); RTL layout correctness is an acceptance criterion per report (Phase 11 standards apply); official/statutory reports carry school identity + signatory slots (BR-SCH-002/004 patterns). |
| BR-RPT-005 | Heavy reports (config threshold) run queued with notification-on-ready (BR-NOT); results retained N days; interactive reports must meet NF-P5 (≤10 s). |
| BR-RPT-006 | **Scheduling:** authorized users subscribe reports (frequency, parameters, format, recipients limited to users holding the report permission — no permission laundering via subscriptions); every scheduled run logs like a manual run. |
| BR-RPT-007 | Statutory reports (ministry formats) ship in country packs versioned separately (regulator format changes = pack update, not product release). |
| BR-RPT-008 | Report data always reflects committed data only (no drafts, BR-GLB-031); as-of-date parameters use the BR-FEE-008 / BR-GLB-064 position semantics where financial. |

## 4. Workflow

Execution is P1 (permission-gated). Subscription creation for restricted reports requires FM/Principal approval (P2). Pack updates: product-support deployment with changelog.

## 5. User roles

All staff (their permitted reports), Report Administrators (Sys Admin — registry visibility, subscription oversight), Auditor (execution logs), Principal (approval of restricted subscriptions).

## 6. Permissions

Per-report View + Export permissions (registered in doc 06 tree under owning module); subscription management permission; execution-log view (Auditor).

## 7. Database concept

Entities: `ReportDefinition` (registry), `ReportExecution` (who/when/params/rows/duration/output), `ReportSubscription` (+ delivery log refs), `QueuedRun`. The engine is Infrastructure (T-5 decision pending — Phase 9 fixes the rendering technology); definitions map to parameterized queries/views designed in Phase 10.

## 8. Required screens

1. **Report center** — catalog tree by module (permission-filtered), search, favorites, recent runs.
2. Report runner — standard parameter bar (year/grade/section/date pickers per BR-RPT-001), preview, export buttons, queue status for heavy runs.
3. Subscription manager — my subscriptions; admin oversight list.
4. Execution log (Auditor) — filterable audit of runs/exports.

## 9. Validation rules

Mandatory parameters enforced before run; date ranges within permitted years (scope); export blocked without Export permission (view-only rendering allowed); subscription recipients permission-verified at save **and** at each send (revocation-safe); heavy-report thresholds route to queue automatically.

## 10. Reports (about reporting)

Execution/usage statistics (most-used, never-used — catalog hygiene) · Export audit report 🔒 · Subscription inventory · Queue performance (NF-P5 compliance).

## 11. Dashboard widgets

"My favorite reports" quick tiles (all personas); Sys Admin: queue health, failed scheduled runs.

## 12. Notifications

`QueuedReportReady` → runner; `ScheduledReportDelivered/Failed` → subscriber/admin; `RestrictedSubscriptionRequested` → approver.

## 13. Future enhancements

Self-service BI layer (semantic model over the Phase 10 schema, e.g., Power BI embedded); report designer for school-custom layouts; data warehouse for multi-year/multi-school analytics (group future); API data feeds (with Module 36 API management).

## 14. Open questions

1. Rendering technology decision (T-5) due in Phase 9 — candidate criteria fixed: bilingual RTL PDF fidelity, Arabic font embedding, template maintainability. |
2. Excel export fidelity: formatted XLSX (proposed for management reports) vs raw-data CSV (data exports) — both, per definition flag; confirm. |
3. Retention of generated report files (proposed 30 days, statutory outputs 1 year) — confirm storage budget. |
