# Module 11 — Parent Management

**Phase:** 4 — People | **Status:** Draft for review | **Rule prefix:** `BR-PAR`

---

## 1. Purpose

Manage parents as **independent, deduplicated entities** — one parent record regardless of how many children, applications, or years — carrying contact identity, financial responsibility, portal access, and the consolidated family view (one login, one statement, one communication channel per family).

## 2. Scope

**In:** parent master record, deduplication engine & merge tool, parent–student links (with Module 10), financial responsibility & consolidated family statement (view over Modules 19–21), portal account lifecycle, communication preferences, custody restrictions, parent directory & data quality.
**Out:** guardianship legal documents (Module 10 owns the link + doc 10 stores files), fee posting (Module 19), messaging content (Module 32).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-PAR-001 | A parent is a person entity (BR-GLB-002) with a permanent Parent File No. (doc 08): bilingual names, ID(s), phones, email, address, occupation/employer (optional), preferred language & channel. Never duplicated per child or per year. |
| BR-PAR-002 | **Deduplication (BR-GLB-003/004):** every creation path (admission portal, counter, import) runs matching — exact on ID numbers; strong on normalized phone; fuzzy on name+other-signals presented as candidates. Exact match blocks creation (link instead); candidates require explicit "not the same person" dismissal (logged). |
| BR-PAR-003 | **Merge tool:** discovered duplicates merge under Registrar permission with preview (links, finance, portal accounts re-pointed to survivor; loser record tombstoned referencing survivor); merges are T1-audited and irreversible-flagged (support-level unmerge only). |
| BR-PAR-004 | Parent–student links carry relationship + flags per BR-STU-003. A parent's **family view** = all linked students across years (and across schools within a group — future per BR-SCH-007) minus custody-revoked links (BR-SEC-011). |
| BR-PAR-005 | **Financial responsibility:** each student has ≥ 1 financially-responsible parent; the family statement consolidates all children's charges/payments per responsible parent; split responsibility (divorced parents each covering specific children or percentages) is supported per child (percentages per fee category = Future; per-child assignment = v1). |
| BR-PAR-006 | One parent = at most one portal account (BR-SEC account model); account provisioning at first child's registration (BR-ADM-007), deactivation when no active/financially-open child remains (grace: visibility of history per retention policy). |
| BR-PAR-007 | Contact data quality is enforced: primary mobile mandatory + verified (OTP on portal activation); bounced channels flag the record into the data-quality queue (BR-NOT-006 feed); parents self-update contacts via portal (identity fields: request → Registrar approval). |
| BR-PAR-008 | Custody restrictions (court orders): per parent × student — flags for portal visibility, physical pickup, communication; restricted category 🔒 (BR-GLB-072), Registrar+Principal manage, documents mandatory (doc 10). Pickup-authorization lists consume these flags (gate security use — future device integration). |
| BR-PAR-009 | Parent identity fields T1-audited; links and flags T1 (they control money and child access); contact fields T2. |
| BR-PAR-010 | Parents with no remaining links (all children left; retention elapsed) follow purge per BR-ATT-011/BR-STU-009 alignment. |

## 4. Workflow

Creation via Admissions dedup path (BR-ADM-003) or Registrar direct (same dedup). Merge: Registrar executes with preview (P1 with T1 audit; no approval chain — speed matters in re-registration season; see Q2). Custody restriction changes: Registrar propose → Principal approve (P2) 🔒. Contact self-updates: auto-apply + notify; identity self-updates: approval queue.

## 5. User roles

Registrar (owner), Admissions (creation path), Finance (responsibility flags + statements), Principal (custody approvals), all teaching staff (contact view per scope), Parent (portal self-service).

## 6. Permissions

| Action | Roles |
|--------|-------|
| View parent directory | Registrar, Admissions, Finance, Principal; teachers see linked parents of own-scope students (contact card only) |
| Create/edit parents | Registrar, Admissions (dedup-enforced) |
| Merge records | Registrar (permission flag, T1) |
| Manage links & flags | Registrar; financial flags + Finance |
| Custody restrictions 🔒 | Registrar + Principal (P2) |
| Provision/reset portal accounts | Registrar, Sys Admin |
| Family statement view | Finance, Registrar, the parent (own) |

## 7. Database concept

Entities: `Parent` (person identity, file no., contacts, preferences, status); `ParentStudentLink` (Module 10's StudentGuardianLink — one table serves both perspectives: parent-side and student-side); `ParentMergeLog` (survivor, tombstone, snapshot); `CustodyRestriction` (link-scoped flags + document refs 🔒); `DedupCandidate` (match runs, dismissals). Family statement and communication history are views over finance/notification data keyed by parent. Portal account links 1:1 to Parent (doc 06 account model).

## 8. Required screens

1. Parent directory — search (name/ID/phone/child), filters (multi-child, balance-due, data-quality flags), export-gated.
2. **Parent file** — tabs: Identity & contacts · Children (links, flags, custody 🔒) · Family statement (consolidated finance read-through) · Communications history (doc 09 log) · Portal account · Documents · Audit.
3. **Dedup workbench** — candidate pairs queue, side-by-side compare, link/dismiss actions; merge wizard with preview diff.
4. Custody restriction manager 🔒 — per link flags + documents + approval trail.
5. Portal: My family (children cards → student profiles), My statement (all children, pay-ready layout), My contacts (self-service), preferences (language, channels, opt-outs per BR-NOT-007).

## 9. Validation rules

Primary mobile mandatory + format per country; email format; ID uniqueness (hard block, BR-PAR-002); at least one financially-responsible link per active student enforced at link editing (removing the last one blocked); merge preview must resolve conflicting field values explicitly; custody flags require document attach; portal account email/phone must be verified before activation.

## 10. Reports

Family register (parents with children count, balances) · Duplicate-candidate report (pending dedup queue age) · Custody exceptions register 🔒 · Contact data-quality report (unverified/bounced) · Multi-school family report (group future) · Portal adoption report (activated/active accounts %) · Financially-responsible mapping report (Finance reconciliation).

## 11. Dashboard widgets

Registrar: dedup queue depth, unverified contacts count, custody approvals pending. Finance: families with consolidated balance > threshold, statements sent this month. Portal (parent home): children summary cards, total due, unread messages.

## 12. Notifications

`PortalAccountActivated` → parent; `ContactChanged` (self-service) → parent (security confirmation) + Registrar log; `IdentityChangeRequested` → Registrar; `CustodyRestrictionApplied` 🔒 → Registrar, Principal (never the restricted parent via system default — school handles legally); `DuplicateCandidateFound` → Registrar queue.

## 13. Future enhancements

Percentage-based split billing per fee category; parent mobile app identity (with portal app); WhatsApp-verified contact channel; sponsor entities (companies/embassies paying fees for N students — flagged **likely v1.x need in Gulf market**, coordinates Module 19/21); family relationship graph (step-parents, multiple guardians UI).

## 14. Open questions

1. **Sponsor/company payers** (embassy, employer pays fees): common in target market — confirm v1.x vs v1 (affects Module 19/21 payer model; recommend v1 keeps parent-payer only, design payer-abstraction note carried to Phase 6). |
2. Merge without approval chain (speed) vs P2 — proposed direct-with-audit; confirm risk appetite. |
3. Should both parents get portal accounts by default, or primary-contact only with opt-in for second? Recommendation: **both by default** (transparency), custody flags handle exceptions — confirm school-policy variance. |
4. Employer/occupation fields: needed for scholarship/discount policies at some schools? Kept optional. |
