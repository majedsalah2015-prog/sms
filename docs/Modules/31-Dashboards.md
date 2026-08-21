# Module 31 — Dashboards

**Phase:** 8 — Platform | **Status:** Draft for review | **Rule prefix:** `BR-DSH`

> Widget inventories per persona were collected in every module doc (§11 sections). The consolidated dashboard **specifications** (widget → data source → drill path per persona) are a Phase 9 deliverable alongside the report catalog.

---

## 1. Purpose

Role-based landing pages that answer "what needs my attention today" in one glance: a widget framework with per-persona default layouts (Principal, VP, Registrar, Finance, Teacher, Parent, Student + the specialized roles), scope-aware data, and drill-through to the owning screens.

## 2. Scope

**In:** widget framework (KPI tile, trend chart, list/queue, alert strip, quick links), persona default layouts (product-defined, school-adjustable), user-level personalization within permission bounds, refresh policies (live/cached-interval per widget class), drill-through contract (every number clicks to its source screen/report), portal dashboards (parent/student home).
**Out:** free-form BI dashboards (Future with BR-RPT self-service), widget content definitions (owned by each module's §11, consolidated Phase 9).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-DSH-001 | Widgets are registered components: code (DSH-<MOD>-###), owning module, required permission, scope behavior, refresh class, drill target. A widget invisible to a user's permissions simply doesn't render (deny-by-default, BR-GLB-070). |
| BR-DSH-002 | **Data honesty:** every widget's number equals what its drill-through screen/report shows for the same scope and moment (one computation source — e.g., attendance % per BR-ATD-009, positions per BR-FEE-008); cached widgets display their as-of time. |
| BR-DSH-003 | Persona defaults ship per role template (doc 06 §4.3); schools adjust defaults (Sys Admin); users personalize (add/remove/arrange permitted widgets); reset-to-default always available. |
| BR-DSH-004 | Approval/queue widgets (My Approvals, uncaptured sections, pending sheets) are **action widgets** — counts with direct action links (doc 05 inbox integration); they refresh live. |
| BR-DSH-005 | Working-year context (BR-AYR-010) governs all widgets; cross-year comparison widgets label years explicitly. |
| BR-DSH-006 | Portal dashboards show only published/committed family-scoped data (BR-SEC-012/BR-SEC-011); no staff widget is portal-reachable. |
| BR-DSH-007 | Restricted-category KPIs (discipline, medical, salary) appear only on explicitly-granted dashboards and never in school-wide aggregate widgets below the configured anonymity threshold (small-count disclosure guard, e.g., "fewer than 5" masking). |

## 4. Workflow

None (consumption surface). Layout administration is direct config (T3-audited).

## 5. User roles

Everyone gets a dashboard; layouts per: Principal, Vice Principal, Registrar, Admissions, Finance Manager, Cashier/Collection, HR, Academic Deputy, HoD, Teacher, Homeroom, Nurse, Librarian, Storekeeper, Cafeteria Supervisor, Transport Supervisor, Discipline Officer, Activities Coordinator, Sys Admin, Auditor, Parent, Student.

## 6. Permissions

Widget-level (BR-DSH-001); layout administration (Sys Admin); personalization (all users, own dashboard).

## 7. Database concept

Entities: `WidgetDefinition` (registry), `LayoutTemplate` (role defaults per school), `UserLayout` (personalization), widget data via module services/materialized views (Phase 10 defines the read models; heavy KPIs precomputed on schedule).

## 8. Required screens

1. Dashboard shell — responsive grid, widget chrome (title, as-of, drill link), personalization mode.
2. Layout administrator — role template editor with preview-as-role.
3. Portal home (parent) — children cards, dues, today (timetable/attendance), unread, calendar strip.
4. Portal home (student) — today's schedule, published results, library/activities.

## 9. Validation rules

Personalization cannot add unpermitted widgets (server-enforced); layout templates validated against role permissions at save (warning on widgets most role members can't see); anonymity threshold enforced in aggregate widgets (BR-DSH-007).

## 10. Reports

Dashboard usage analytics (widgets viewed/drilled — informs product); stale-cache monitor (Sys Admin).

## 11. Dashboard widgets (about dashboards)

Sys Admin: cache/refresh health, layout template coverage.

## 12. Notifications

None own; action widgets surface doc 05/09 items.

## 13. Future enhancements

KPI targets & thresholds with alert subscriptions (Principal sets "alert me if attendance < 90%"); group-level consolidated dashboards (multi-school); TV/kiosk mode (staff-room screens); natural-language KPI Q&A (product-level AI roadmap item — governance first).

## 14. Open questions

1. Default refresh classes (live vs 15-min cache vs daily) per widget family — proposed: action widgets live, finance/attendance 15-min, trends daily; confirm infra appetite. |
2. Anonymity threshold default (proposed <5 masking) — confirm with privacy stance per pack. |
3. Student dashboard minimum stage (ties doc 06 Q2 student accounts) — same config, confirm. |
