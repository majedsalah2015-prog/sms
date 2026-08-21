# Module 36 — System Administration

**Phase:** 8 — Platform | **Status:** Draft for review | **Rule prefix:** `BR-SYS`

---

## 1. Purpose

The IT admin's home: user account lifecycle and the doc 06 security surfaces (roles, scopes, policies), background-job operations, health monitoring, data-management utilities (imports, purges), license/subscription visibility, and the support/diagnostics interface — completing the platform alongside Modules 33–35.

## 2. Scope

**In:** user management (staff/parent/student accounts per doc 06 §2 — provisioning batches, lifecycle, resets, 2FA), role & permission administration (doc 06 §8 screens realized), session management, background-job console, system health monitoring (queues, storage, integration endpoints), **data import framework** (onboarding: legacy students/parents/marks/finance opening balances — dry-run mandatory per BR-STU-010), retention/purge orchestration (executing framework schedules with certificates), license & subscription status (BR-SET Q3), environment info & diagnostics bundle for support, announcement banner (maintenance notices).
**Out:** business configuration (Module 01), backup execution (Module 35), audit consumption (Module 34), notification ops (Module 33).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-SYS-001 | **Account lifecycle** follows doc 06 §2 strictly: creation only linked to a person entity; batch provisioning (parents at admission waves, students by stage policy) with activation links; deactivation automatic on the owning module events (offboarding/withdrawal) plus manual with reason; dormant-account review per BR-SEC-022 feeds a quarterly cleanup queue. |
| BR-SYS-002 | Security administration realizes doc 06 rules verbatim: role designer, scope assignment, permission matrix reporting, the two-admin approval for security-grant changes (doc 06 §4.3), impersonation per BR-SEC-020. This module adds no new security semantics. |
| BR-SYS-003 | **Import framework:** typed import templates (students+parents, employees, marks history, opening balances, library catalog) with: schema validation, dedup engine engagement (BR-PAR-002 — imports never bypass), dry-run diff mandatory, commit under a labeled batch (pre-op snapshot per BR-BAK-004), per-row audit (BR-STU-010), rollback window (batch reversal while no dependent transactions exist). |
| BR-SYS-004 | **Job console:** all background jobs (notifications, report queue, late-fee runs, closures, verification, materialized refreshes) visible with schedules, last-run status, and manual-trigger permission per job class; failed jobs alert (doc 09 `JobFailed`) and list until resolved. |
| BR-SYS-005 | **Purge orchestration:** executes framework retention schedules (BR-ATT-011, BR-STU-009, BR-AUD-006 alignment) as certified jobs with dual confirmation (Sys Admin + Registrar/Auditor per data class) and legal-hold respect. |
| BR-SYS-006 | **License:** student-count and module-toggle entitlements visible (enforcement per commercial model — BR-SET Q3 pending); approaching-limit warnings; expiry grace behavior defined (read-only degradation, never data lockout — product ethics stance). |
| BR-SYS-007 | Maintenance mode: planned-downtime banner + portal notice scheduling; emergency read-only mode toggle (support-gated) for incident containment. |
| BR-SYS-008 | Diagnostics bundle (logs excerpt, config snapshot, health metrics — **no personal data**) generated for support tickets; generation logged. |

## 4. Workflow

Security grants: two-admin P2 (doc 06). Imports: dry-run → commit (P2 for finance-bearing imports). Purges: dual-confirm certified jobs. License changes: product-support. All else direct Sys Admin operations (audited per framework tiers).

## 5. User roles

System Administrator (owner), Product Support (license, diagnostics, restores liaison), Registrar/HR (account batch triggers via their modules), Auditor (registers), Principal (visibility reports).

## 6. Permissions

User admin · Security admin (two-admin gated) · Job console (view/trigger per class) · Imports (per template + finance P2) · Purge execution (dual) · License view · Maintenance mode (support-gated) — all under the doc 06 tree; Sys Admin role itself cannot self-expand (BR-SEC governance).

## 7. Database concept

Entities: `UserAccount` (doc 06 model), `ProvisioningBatch`, `ImportBatch` (+ rows, dry-run results, reversal state), `JobDefinition`/`JobRun`, `HealthMetricSample`, `PurgeExecution` (+ certificates), `LicenseState`, `MaintenanceWindow`, `DiagnosticsBundle` (metadata). Role/permission entities per doc 06 (Phase 10 formalizes).

## 8. Required screens

1. User management — directory, lifecycle actions, batch provisioning wizards, reset/2FA tools, dormant queue.
2. Role & permission center — role designer (permission tree with verbs per doc 06 §4.1), scope assignment, matrix reports, two-admin approval flow.
3. **Import workbench** — template download, upload, validation report, dedup review (BR-PAR workbench embed), dry-run diff, commit/rollback.
4. Job console — schedule board, run history, failure queue.
5. Health dashboard — queues, storage, endpoint checks, performance counters (NF-P targets tracked).
6. Purge center — schedules per data class, dual-confirm execution, certificates.
7. License panel — entitlements, usage vs tier, expiry.
8. Maintenance & diagnostics — windows, banners, read-only toggle, bundle generator.

## 9. Validation rules

Accounts only person-linked; batch activation links expire (config); imports: schema + dedup + dry-run gates hard (no direct-commit path exists); job manual triggers respect dependency locks (no late-fee run during day-close); purge blocked by legal holds; license warnings non-blocking until grace end (then read-only per BR-SYS-006); maintenance banner mandatory lead time (config) except emergency.

## 10. Reports

User/account inventory & dormant report (BR-SEC-022) · Role-permission matrix & change register · Import batch register (with per-batch audit links) · Job reliability report · Health/performance trend (NF compliance evidence) · Purge certificates register · License usage history.

## 11. Dashboard widgets

Sys Admin (the operational home): failed jobs, health traffic lights, pending security approvals, dormant accounts, import batches in progress, license usage bar. Principal: license tile, uptime tile.

## 12. Notifications

`JobFailed` → Sys Admin; `SecurityGrantPending` → second admin; `LicenseThreshold/Expiry` → Sys Admin + Principal; `ImportCommitted` → data owners (Registrar/FM per template); `MaintenanceScheduled` → all staff + portal; `HealthDegraded` → Sys Admin (+Support per severity).

## 13. Future enhancements

SSO administration (doc 06 Future); public API key management & webhooks (integration platform); multi-school group administration console (tenant provisioning, cross-school roles); self-service sandbox refresh (copy-anonymized for training); configuration change review/rollback UI (git-style config history).

## 14. Open questions

1. License enforcement mechanics (BR-SET Q3): student-count soft/hard tiers, module toggles as commercial SKUs — commercial model decision needed before implementation (not blocking analysis). |
2. Import scope for v1 onboarding: proposed template list (students+parents, employees, opening balances, marks history, library) — confirm marks-history depth (summary per year vs full component marks; **recommend summary-only** — full history import is an onboarding cost trap). |
3. Read-only degradation on license expiry (BR-SYS-006) — confirm commercial acceptance (sales may want harder enforcement; product ethics argue data access preservation). |
