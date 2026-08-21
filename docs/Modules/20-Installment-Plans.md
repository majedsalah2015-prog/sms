# Module 20 — Installment Plans

**Phase:** 6 — Finance | **Status:** Draft for review | **Rule prefix:** `BR-INS`

---

## 1. Purpose

Turn posted charges into collectible, dated schedules: school-defined installment plan templates, per-family plan assignment, due-date management, rescheduling under control, and the dunning ladder that drives fee-due notifications and late-fee runs.

## 2. Scope

**In:** plan templates (e.g., annual/2 semesters/4 installments/10 monthly), plan assignment per student (default per school/grade + per-family exceptions), schedule generation against charges, due-date calendar rules, rescheduling workflow, promise-to-pay tracking, dunning ladder configuration, installment status lifecycle.
**Out:** the charges themselves (Module 19), collection & allocation of money (Module 21 — pays installments), late-fee computation (Module 19 policy consumes overdue facts from here), discounts (Module 22 — reduce scheduled amounts via allocation rules).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-INS-001 | **Plan templates** per school per year: name (bilingual), installment count, percentage or fixed split per installment, due-date rules (absolute dates or offsets from year start/term starts), category applicability (tuition typically; transport optionally its own plan), down-payment requirement (% at registration). Templates approved with the fee structure (P3, BR-FEE workflow alignment). |
| BR-INS-002 | **Assignment:** each student-year gets one plan per applicable category group — default template per grade (school config) applied at charge generation; per-family exceptions (different template) permission-gated (Finance Manager); assignment generates the **installment schedule**: dated amounts summing exactly to net scheduled charges (rounding differences absorbed in last installment — explicit rule). |
| BR-INS-003 | Schedules recompute only through controlled events: new charges (service added mid-year → appended/merged per policy), credit notes/discounts (reduce future installments first, then last-to-first — config), plan change (§BR-INS-005). Paid installments never mutate (BR-GLB-062 spirit); recomputations logged T1 with before/after snapshot. |
| BR-INS-004 | Due dates falling on non-working days shift per BR-CAL-003 policy (next working day default); due-date calendar visible to payers from assignment moment (portal). |
| BR-INS-005 | **Rescheduling** (family hardship, negotiation): new schedule proposal for **unpaid remainder only** → WF (P3: Cashier/officer proposes → Finance Manager approves; beyond N months extension or crossing year-end → + Principal P4); old schedule superseded (kept in history); reschedule count per family reported (abuse signal). |
| BR-INS-006 | **Promise-to-pay:** dated commitment recorded against overdue installments (by cashier or portal); breaks (date passed unpaid) escalate the dunning ladder automatically. |
| BR-INS-007 | **Installment status:** `Scheduled → Due → Overdue (grace elapsed) → Paid / PartiallyPaid / Rescheduled / Written-off (WF-06)`; status derives from allocations (Module 21) and dates — never manually set. |
| BR-INS-008 | **Dunning ladder** config: reminder D-7/D-1 (BR-NOT catalog), overdue notices at +3/+14/+30 (tone escalating, bilingual templates), then flag stages: portal banner → statement letter (numbered, Module 18 pattern) → escalation list for management action (service suspension decisions are **human**, policy-gated per school: e.g., transport suspension allowed, academic exclusion governed by country pack legality — mirrors BR-CRT-008 caution). |
| BR-INS-009 | Post-dated cheque (PDC) coverage: installments may be marked PDC-covered when cheques are lodged (Module 21 PDC registry); PDC-covered ≠ Paid (paid only on clearance) but suppresses dunning per config. |
| BR-INS-010 | Plan/schedule data T1-audited (money-adjacent); dunning sends logged per BR-NOT-006. |

## 4. Workflow

Template approval with fee structures (P3). Assignment: automatic default (P1) / exception (permission). Reschedule: P3/P4 per BR-INS-005. Write-off: WF-06 (P4). Dunning: automated ladder with human gates at flag stages (Finance confirms letter batches).

## 5. User roles

Finance Manager (owner), Cashier/Collection Officer (day-to-day: promises, reschedule proposals), Principal (escalation approvals), Registrar (view), Parent (portal schedule + promise), Auditor.

## 6. Permissions

| Action | Roles |
|--------|-------|
| Configure templates | Finance Manager (+P3) |
| Assign exceptions | Finance Manager |
| Propose/approve reschedules | Officer → FM (→ Principal per P4 rule) |
| Record promises | Cashier, Officer; Parent (portal, config) |
| Confirm dunning letter batches | Finance Manager |
| Suspension-list decisions | Principal (policy-gated) |
| View schedules | Finance, Registrar; Parent (own) |

## 7. Database concept

Entities: `PlanTemplate` + `TemplateInstallment` (split rules); `PlanAssignment` (student-year × category group × template, exception flag); `Installment` (dated amount, status-derived, charge allocations map); `RescheduleCase` (workflow, superseded schedule snapshot); `PromiseToPay`; `DunningEvent` (ladder step, sent refs); PDC linkage refs (Module 21). Installment↔Charge is many-to-many via scheduled-allocation lines (a schedule spans multiple charges; Phase 10 formalizes). Status derivation from Module 21 allocations keeps one source of payment truth.

## 8. Required screens

1. Template designer — split builder with live preview against a sample structure, due-date rules.
2. Assignment console — defaults per grade, exception assignment with reason.
3. **Family schedule view** — per payer: all children's installments merged timeline, statuses, promises, PDC flags (the collection officer's main screen; shared position drill with Module 19/21).
4. Reschedule wizard — remainder selection, new split proposal, chain submission, history compare.
5. Dunning console — ladder config, pending letter batches, escalation list (with policy flags), promise-break queue.
6. Portal: my payment schedule (family timeline), promise-to-pay request (config), reminders opt-in state.

## 9. Validation rules

Template splits sum to 100%/full amount; schedule sum = scheduled charges exactly (rounding rule BR-INS-002); reschedule covers full unpaid remainder (no orphan amounts); promise dates ≥ today and ≤ config horizon; dunning steps only fire on truly-overdue derived status (PDC suppression per BR-INS-009); exception assignments require reason. |

## 10. Reports

Collection calendar (expected inflow by week/month — cashflow forecast) · Overdue installments by payer/grade/bucket (with BR-FEE aging alignment) · Reschedule register (count per family, terms) · Promise-to-pay outcomes (kept/broken %) · Dunning effectiveness (paid within N days of each step) · PDC-covered vs open exposure · Plan distribution (families per template) · Suspension-candidate list (policy-gated) 🔒. |

## 11. Dashboard widgets

Finance Manager: this month expected vs collected, overdue total + top-10 payers, broken promises today. Collection Officer: today's follow-up queue (promises due, new overdues). Principal: escalation list size, collection rate trend. Portal: next installment card with pay-CTA placeholder (gateway future).

## 12. Notifications

`InstallmentDueSoon` (D-7/D-1) → payer; `InstallmentOverdue` (+3/+14/+30 escalating) → payer (+Finance at +30); `PromiseRecorded/PromiseBroken` → officer (+payer confirmation); `RescheduleDecided` → payer; `DunningLetterIssued` → payer (formal); `PDCClearedAppliedToInstallment` → payer (via Module 21 event). |

## 13. Future enhancements

Auto-pay mandates (card-on-file/direct debit with gateway); dynamic plan pricing (early-bird annual discount linkage with Module 22); collection scoring (propensity models); sponsor-payer schedules (activates with BR-FEE-004 sponsor billing); installment insurance products (market-dependent).

## 14. Open questions

1. Default dunning ladder timings/tones — proposed above; confirm against school culture per market (aggressiveness varies). |
2. Service-suspension policy defaults (BR-INS-008): which services may suspend for arrears out of the box (transport yes / exams per pack / academic reports per pack)? Needs the same legal input as BR-CRT-008 Q1. |
3. Down-payment enforcement at registration (BR-INS-001): blocking (no registration without %) or configurable? Proposed: configurable, default blocking for new admissions, off for re-registration. |
4. Portal promise-to-pay: enabled by default? Proposed: off (counter-only) until schools trust the flow. |
