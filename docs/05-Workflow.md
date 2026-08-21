# 05 — Workflow Framework

**Phase:** 2 — Cross-cutting frameworks | **Status:** Draft for review | **Owner:** Software Architect + Senior Business Analyst

> One workflow engine serves all modules. Module documents (Phases 3–8) declare *which* workflows they use and their specific states/steps; this document defines the framework every such declaration must fit.

---

## 1. Purpose

Provide a single, configurable mechanism for: (a) **status lifecycles** (every managed entity moves through defined states only), and (b) **approval chains** (sensitive operations require one or more approvals before taking effect), with delegation, escalation, and full audit — without custom code per school.

## 2. Core concepts

| Concept | Definition |
|---------|------------|
| Workflow Definition | A named state machine attached to an entity type (e.g., Admission Application), versioned per school |
| State | A named status (bilingual) with flags: initial, final, editable-in-state, visible-to-portal |
| Transition | Allowed movement state→state, bound to an **action** (Submit, Approve, Reject, Return, Cancel…), a permission, and optional conditions |
| Condition | Configurable predicate evaluated at transition (e.g., discount % > threshold → route to Principal) |
| Approval Step | A transition requiring sign-off by a role/user; chains may be sequential (multi-level) |
| Actor Resolution | Who may act: role-based, scoped by the record's school/year/grade/section (doc 06 scopes apply) |
| Delegation | Time-boxed transfer of approval authority (vacation cover); always visible in audit |
| Escalation | SLA per step; overdue items notify the approver, then escalate to the fallback role |
| Reason Policy | Per transition: reason optional / required / required-from-list (+ free text) |

## 3. Standard state vocabulary

Modules must reuse these state names (bilingual) where semantics match; new states require justification in the module doc:

`Draft → Submitted → Under Review → Approved | Rejected | Returned (to submitter) → Active/Posted → Completed/Closed → Cancelled`

Rules of the vocabulary:

- **Draft** never affects other modules (BR-GLB-031).
- **Returned** goes back to the submitter for correction and resubmission (loop allowed, count audited).
- **Rejected** is final for that request; a new request must be created.
- **Cancelled** keeps number + history + mandatory reason (BR-GLB-032).

## 4. Approval patterns

| Pattern | Use | Example |
|---------|-----|---------|
| P1 — Direct lifecycle | No approval; status tracks progress | Library loan |
| P2 — Single approval | One approver role | Leave pass (student permission) |
| P3 — Multi-level sequential | 2+ approvers in order | Refund: Finance Officer → Principal |
| P4 — Threshold routing | Approval chain depends on value | Discount ≤10% Finance Head; >10% + Principal; >25% + Owner |
| P5 — Committee/parallel | N-of-M approvers (v1: sequential emulation; true parallel = Future) | Discipline board decision |

## 5. Workflow catalog (v1 — refined per module in Phases 3–8)

| # | Workflow | Entity | Pattern | Final effect |
|---|----------|--------|---------|--------------|
| WF-01 | Admission application | Applicant | P3 + P4 (seat availability) | Student created, number generated, fees generated |
| WF-02 | Re-registration | Enrollment (next year) | P2 | Enrollment confirmed, fees generated |
| WF-03 | Student withdrawal | Student | P3 (clearance: library/store/finance/transport) | Status Withdrawn, transfer certificate issuable |
| WF-04 | Discount / scholarship grant | Discount | P4 | Discount applied to student fees |
| WF-05 | Refund | Refund voucher | P3 | Payment reversal posted, voucher numbered |
| WF-06 | Fee write-off | Adjustment | P4 | Receivable reduced |
| WF-07 | Marks approval | Marks sheet (subject×section×exam) | P3 (Teacher submit → Head of Dept → Registrar publish) | Results locked & publishable |
| WF-08 | Post-publication mark change | Mark | P4 (always Principal) | Mark corrected, reason mandatory, parent re-notified |
| WF-09 | Certificate issuance | Certificate | P2 | Numbered certificate issued |
| WF-10 | Employee leave request | Leave | P3 (Manager → HR) | Leave balance deducted |
| WF-11 | Discipline case | Incident | P3/P5 + severity routing | Action applied, parent notified, appeal window |
| WF-12 | Timetable publication | Timetable version | P2 | Timetable active for year/term |
| WF-13 | Closed-year posting | Any transaction in closed year | P2 (always) | Posting allowed once, audited (BR-GLB-022) |
| WF-14 | Attendance correction (past day) | Attendance record | P2 | Record corrected with reason |
| WF-15 | Purchase/void on store & cafeteria | Sale void | P2 | Sale voided, stock restored |

## 6. Business rules

| ID | Rule |
|----|------|
| BR-WF-001 | State transitions occur only through defined workflow actions; direct status edits are impossible for all roles. |
| BR-WF-002 | Every transition records actor, UTC timestamp, action, reason (per reason policy), and before/after state in the audit trail. |
| BR-WF-003 | An approver cannot approve their own submission (segregation of duties); the engine blocks self-approval even when roles overlap. |
| BR-WF-004 | Approval authority is scope-checked: the approver's data scopes (school/year/grade/section) must cover the record. |
| BR-WF-005 | Threshold routing (P4) evaluates against configured school thresholds; thresholds are versioned per academic year. |
| BR-WF-006 | Delegation is time-boxed, cannot exceed the delegator's own authority, and is disclosed on every action taken under it. |
| BR-WF-007 | Escalation SLAs are per step; overdue steps notify daily and escalate after the configured period. |
| BR-WF-008 | Workflow definitions are versioned; in-flight instances complete on the version they started (no retroactive redefinition). |
| BR-WF-009 | Final effects (create student, post discount, lock marks) execute atomically with the final approval — approved-but-not-applied states must be impossible. |
| BR-WF-010 | Rejection and Return always require a reason; the submitter is notified with it. |
| BR-WF-011 | Pending approvals appear in the approver's unified inbox ("My Approvals") across all modules. |

## 7. User-facing components

| Component | Description |
|-----------|-------------|
| My Approvals inbox | Unified pending-actions list: entity, requester, age, SLA state; approve/reject/return with reason inline |
| Request tracker | Submitter's view of their requests and current step |
| Workflow history panel | Timeline on every workflow-managed record: steps, actors, reasons |
| Workflow admin screens | Definition designer (states, transitions, thresholds, SLAs, delegation), simulation preview |

## 8. Notifications integration (doc 09)

Standard events raised by the engine: `StepAssigned`, `StepOverdue`, `Escalated`, `Approved`, `Rejected`, `Returned`, `Cancelled`. Module docs subscribe recipients to these.

## 9. Reports & widgets

- Pending approvals by module/role/age (SLA breach highlighted)
- Approval throughput (avg time per step, per approver)
- Rejection/return rates by workflow (quality signal)
- Widget: "Awaiting my action" count per persona dashboard

## 10. Future enhancements

True parallel/committee voting, mobile push approvals, workflow analytics (bottleneck detection), per-school visual designer for non-standard workflows.

## 11. Open questions

1. WF-03 clearance: is clearance sequential (finance last) or parallel checklist? Recommendation: parallel checklist with finance veto — confirm in Student module.
2. Should parents see workflow states of their requests (e.g., admission "Under Review") verbatim or via simplified portal states? Recommendation: simplified mapping per workflow.
3. Default SLA values per step — to be proposed per module and confirmed by pilot school.
