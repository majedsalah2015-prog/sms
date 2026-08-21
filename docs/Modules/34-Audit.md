# Module 34 — Audit (Administration)

**Phase:** 8 — Platform | **Status:** Draft for review | **Rule prefix:** `BR-AUM`

> The audit **framework** (domains, tiers, capture rules BR-AUD-###) is approved doc [07](../07-Audit.md). This module is its **operations and consumption surface**.

---

## 1. Purpose

Make the audit trail usable and provably intact: the explorer and record-history experiences, security-event monitoring with anomaly highlights, integrity verification operations (hash-chain checkpoints), retention execution, and the auditor's working toolkit.

## 2. Scope

**In:** audit explorer & record history panel (doc 07 §6 screens realized), security event console, integrity dashboard & verification runs, retention/purge execution for audit data (per BR-AUD-006 — audit outlives operational purges), auditor workspace (saved investigations, export with chain-of-custody note), tier-configuration view (raise-only per BR-AUD-002), anomaly rule configuration.
**Out:** capture mechanics (framework, in every module's implementation), operational-data retention (Module 36/frameworks own schedules; this module executes audit-side only).

## 3. Business rules (operational additions to BR-AUD-###)

| ID | Rule |
|----|------|
| BR-AUM-001 | Integrity verification runs on schedule (daily default per BR-AUD-007) and on demand; failures raise an urgent Sys Admin + Auditor alert and freeze audit-affecting maintenance jobs until investigated. |
| BR-AUM-002 | Anomaly rules are configurable detections over audit streams: out-of-hours admin actions, mark changes within N days of certificate issuance, bulk personal-data exports, repeated failed logins with subsequent success, permission self-elevation attempts; rule hits create review items (Auditor queue), never automatic reversals. |
| BR-AUM-003 | Auditor exports carry a chain-of-custody cover (who exported, when, filter set, integrity checkpoint reference) and are themselves T0-audited (BR-AUD-004 recursion accepted by design). |
| BR-AUM-004 | Tier configuration UI enforces raise-only (BR-AUD-002) with product-floor display; changes T1. |
| BR-AUM-005 | Audit-data purge (end of BR-AUD-006 horizon) runs as a certified job (mirror BR-ATT-011 purge-certificate pattern) and never touches integrity checkpoints of retained ranges. |

## 4. Workflow

Verification runs (scheduled/system). Anomaly review: item → Auditor disposition (dismiss with note / escalate to Principal case) — P1 logged. Purge jobs: Sys Admin + Auditor dual-confirmation (P2-style double control).

## 5. User roles

Auditor (primary consumer), Sys Admin (operations), Principal (escalations, sensitive-change register per doc 07 reports), module managers (record-history panels within their scopes — everyone sees history where they see the record, per BR-AUD-008).

## 6. Permissions

Audit explorer (Auditor; module-scoped audit views per module grants) · Security console (Auditor + Sys Admin) · Integrity ops (Sys Admin, results visible to Auditor) · Anomaly config (Auditor + Principal P2) · Exports (Auditor, T0).

## 7. Database concept

Consumes doc 07 stores; adds: `AnomalyRule` + `AnomalyHit` (queue state), `VerificationRun` (results per checkpoint range), `AuditPurgeCertificate`, `SavedInvestigation`. Partitioning/volume strategy per doc 07 §8 lands in Phase 10.

## 8. Required screens

1. **Audit explorer** — cross-entity search (user/date/entity/module/action/school/year), timeline view, diff rendering (old→new bilingual-aware per BR-AUD-005), export (guarded).
2. Record history panel (embedded product-wide — the one-click history per BR-AUD-008).
3. Security event console — streams with anomaly badges, session/lockout views.
4. Integrity dashboard — checkpoint status map, verification run history, failure drill.
5. Anomaly queue — hits with context bundles, disposition actions.
6. Retention console — horizons per data class, upcoming purges, certificates.

## 9. Validation rules

Explorer queries require at least one narrowing filter (no full-dump browsing; exports define explicit filter sets); anomaly dispositions require note; purge dual-confirmation with certificate preview; tier changes raise-only enforced server-side.

## 10. Reports

(Realizing doc 07 §7) sensitive-change register · mark-change post-publication report · discount/refund chains · export log · anomaly outcomes · integrity attestation report (periodic signed statement for external audit) · audit volume/health.

## 11. Dashboard widgets

Auditor: anomaly queue depth, today's sensitive changes, verification status. Sys Admin: audit write health, storage growth. Principal: sensitive-changes-today counter (doc 07 widget).

## 12. Notifications

`IntegrityCheckFailed` → Sys Admin + Auditor urgent; `AnomalyDetected` (rule-class-dependent severity) → Auditor (+ Principal for critical); `PurgeScheduled/Executed` → Auditor; `AuditWriteFailure` → Sys Admin urgent (BR-AUD-003 breach signal).

## 13. Future enhancements

Per doc 07 §9 (SIEM export, ML anomaly detection, legal hold) plus: external-auditor time-boxed portal (doc 06 Q3 dependent); regulator attestation packages per country pack.

## 14. Open questions

1. Anomaly starter rule set (proposed BR-AUM-002 list) — confirm and set default sensitivities with pilot. |
2. Integrity attestation cadence for the signed report (proposed monthly) — align with customer audit committee expectations. |
3. Doc 06 Q3 (external auditor accounts) decision needed before this module's implementation phase. |
