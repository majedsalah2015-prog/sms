# Module 35 — Backup

**Phase:** 8 — Platform | **Status:** Draft for review | **Rule prefix:** `BR-BAK`

---

## 1. Purpose

Meet NF-A2/A3 (RPO ≤ 15 min cloud / ≤ 24 h on-prem, RTO ≤ 4 h) with verifiable, monitored, restorable backups of both database and file storage — surfaced in-product so schools and support can *see* protection status, not assume it.

## 2. Scope

**In:** backup policy configuration (schedules, retention generations, scope: DB + attachment storage + configuration), execution monitoring (in-product status regardless of underlying tooling), **automated restore verification** (NF-A4: scheduled test-restores with content hash checks per BR-ATT-010), pre-operation safety snapshots (before rollover, purges, upgrades), restore request workflow (support-executed), offsite/second-copy status, on-prem guidance pack.
**Out:** infrastructure DR orchestration (cloud provider tooling; documented, not built), end-user file recycle bins (soft-delete already covers, ADR-7).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-BAK-001 | Backup scope must cover: database, attachment store, and configuration/secrets (encrypted) — a backup set is Complete only with all three; partial sets flag Degraded. |
| BR-BAK-002 | Policies per deployment class: cloud (continuous log backup + daily full, retention: 30 daily / 12 monthly / per-pack yearly) and on-prem (product-shipped defaults + customer-acknowledged responsibility statement); retention horizons respect country-pack data law (align BR-AUD-006 — backups containing purged-subject data age out per schedule, documented for DPO answers). |
| BR-BAK-003 | **Verification:** scheduled automated test-restore (weekly default) into an isolated environment with checks: DB restore success, row-count sanity on key tables, attachment hash sampling (BR-ATT-010), checkpoint integrity (BR-AUD-007 alignment); verification results are first-class records — a backup is only Trusted after its generation's last verification passed. |
| BR-BAK-004 | **Pre-operation snapshots:** rollover activation (BR-AYR-004), audit/data purges, version upgrades, and bulk imports (BR-STU-010) automatically take a labeled snapshot first; the initiating operation blocks if the snapshot fails. |
| BR-BAK-005 | **Restore is a support-gated workflow** (never self-service in-product): request (Sys Admin) → scope definition (point-in-time, full vs tenant) → product-support execution → post-restore integrity verification + mandatory gap analysis (transactions since restore point documented to the school). All restores T1-audited with certificates. |
| BR-BAK-006 | Failure handling: missed/failed backup → `BackupFailed` urgent alert (doc 09 catalog) + support ticket auto-flag; two consecutive failures = product incident severity. |
| BR-BAK-007 | Backups encrypted at rest and in transit; backup access is infrastructure-credentialed (not application roles); the application surfaces status only. |

## 4. Workflow

Execution/verification: scheduled jobs. Restore: BR-BAK-005 chain (dual control: customer request + support execution). Policy changes: Sys Admin + product-support acknowledgment for reductions below defaults (P2-style, T1).

## 5. User roles

Sys Admin (status, requests, policy view), Product Support (execution, restores), Principal (status report visibility — owner assurance), Auditor (restore/verification registers).

## 6. Permissions

Status dashboard (Sys Admin, Principal read) · Policy view/change-request (Sys Admin) · Restore requests (Sys Admin) · Execution (Product Support only) · Registers (Auditor).

## 7. Database concept

Entities: `BackupPolicy` (per deployment), `BackupRun` (set composition, status, sizes), `VerificationRun` (checks, results), `SnapshotEvent` (pre-operation labels), `RestoreCase` (workflow, gap analysis, certificate). Actual backup artifacts live in infrastructure storage; the module records metadata + status.

## 8. Required screens

1. **Protection status dashboard** — last backup per component, last verified generation, RPO compliance indicator, offsite status; deliberately simple traffic-light design (owner-readable).
2. Run & verification history — timelines, failure drill-downs.
3. Restore request wizard — point-in-time picker with impact preview (data-since-then summary), case tracking.
4. Policy viewer — schedules/retention, on-prem responsibility statement acknowledgment.

## 9. Validation rules

Policy reductions below product minimum blocked (support override only); restore requests require reason + scope; pre-op snapshot success gates the initiating operations (BR-BAK-004); verification failures block Trusted status propagation.

## 10. Reports

Backup compliance report (runs vs policy, per period — customer assurance artifact) · Verification register (NF-A4 evidence) · Restore case register with certificates · Storage consumption trend · RPO/RTO attainment (post-incident reviews).

## 11. Dashboard widgets

Sys Admin: protection traffic light, last verification age, storage trend. Principal: single assurance tile ("Data protected — last verified restore: date").

## 12. Notifications

`BackupFailed` → Sys Admin + Support urgent; `VerificationFailed` → Sys Admin + Support urgent; `RestoreCompleted` → requester + Principal (with gap summary); `RetentionAgeOut` (yearly sets) → Sys Admin FYI; `PolicyChanged` → Principal.

## 13. Future enhancements

Cross-region DR replicas with failover runbooks (cloud tier); customer-downloadable encrypted backup export (data-portability/off-boarding right — likely a contractual requirement worth v1.x); per-tenant export for school-group splits.

## 14. Open questions

1. On-prem reality check: many schools lack IT discipline — should the product **require** cloud backup storage (product-managed, priced-in) even for on-prem deployments? **Recommend yes as default offering**; confirm commercial model. |
2. Verification cadence (weekly proposed) vs storage/compute cost — confirm. |
3. Customer off-boarding data export (Future §13) — contractual commitment needed? Legal input with country packs. |
