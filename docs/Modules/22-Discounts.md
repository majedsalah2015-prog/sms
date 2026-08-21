# Module 22 — Discounts

**Phase:** 6 — Finance | **Status:** Draft for review | **Rule prefix:** `BR-DIS`

---

## 1. Purpose

Control every reduction of revenue: discount types (sibling, staff, early-payment, loyalty, hardship), scholarships/sponsorship grants, waivers (late-fee/misc), all approval-gated per thresholds (WF-04/WF-06 family), fully audited, and reported as the "revenue given away" the owner always asks about.

## 2. Scope

**In:** discount type catalog & policies (eligibility rules, computation basis, caps, stacking rules), automatic-eligibility engine (sibling/staff), manual grants (hardship etc.), scholarship programs (full/partial, sponsor-funded flag — payer abstraction tie-in), waivers, approval thresholds (P4 ladders), application to charges/schedules, renewal per year (no silent carry-over), revocation.
**Out:** posting mechanics (applies via Module 19 credit-style application), sponsor billing itself (BR-FEE-004 future activation), fee-structure pricing (Module 19).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-DIS-001 | **Discount types** cataloged: bilingual name, basis (percentage of category / fixed amount), applicable categories (tuition-only typical), computation stage (before/after VAT per regime — pack-driven), cap per student/family, stacking policy (stackable set + max combined % — global cap default 100% guarded), eligibility mode (automatic rule / manual grant), renewal mode (auto-eligible re-check / manual re-grant per year). |
| BR-DIS-002 | **Automatic eligibility:** sibling discount (Nth-child ladders, e.g., 3rd child 10%, 4th+ 15% — recomputed on family enrollment changes: a withdrawal mid-year triggers recalculation per policy config: keep/adjust-forward); staff discount (linked to active employee parent per BR-EMP, % per policy, ends with employment per config: immediate/term-end/year-end). Automatic grants still post through approval (batch WF-04 P2 — one approval per batch, listed) so the register is complete. |
| BR-DIS-003 | **Manual grants** (hardship, negotiation, owner discretion): always WF-04 threshold-routed (P4: e.g., ≤10% Finance Manager; ≤25% + Principal; >25% + Owner/Board role); hardship documentation restricted 🔒 (BR-GLB-072); mandatory reason + validity scope (year, categories). |
| BR-DIS-004 | **Scholarships:** named programs (academic excellence, orphan sponsorship, Quran memorization… school-defined) with budget envelopes (count or amount caps per year), candidate nomination → committee decision (P5/P3 per school), award = 100%-or-% discount instance flagged Scholarship + optional external sponsor funding note (payer-abstraction future links actual sponsor billing). Budget consumption tracked against envelope (exceeding blocks, override Owner-level). |
| BR-DIS-005 | **Application:** approved discounts apply to the student-year position as discount documents (auditable lines reducing category charges per basis), recompute forward installments per BR-INS-003; discounts never produce negative positions; mid-year grants pro-rate per category policy (BR-FEE-006 alignment). |
| BR-DIS-006 | **Waivers** (late fees, bounce fees, misc): WF-06 family (P2/P4 by amount); waiver register separate from discounts in reporting (operational forgiveness vs pricing policy). |
| BR-DIS-007 | **Annual renewal:** no discount carries into a new year silently (BR-GLB-023 spirit): automatic types re-evaluate at rollover fee generation; manual/scholarship types enter a renewal review queue (approve/adjust/drop) before new-year application. |
| BR-DIS-008 | **Revocation** mid-year (fraud, policy breach, employment end): P2 (Finance Manager + Principal) with effective date; already-consumed portions handled per policy (claw-back charge vs forgive — config, default forgive past). |
| BR-DIS-009 | Everything here is T1-audited with reasons; the **discount register** (who got what, why, approved by whom) is a permanent auditor artifact (BR-GLB-063). |
| BR-DIS-010 | Total-discount visibility: every payer statement and position view shows gross charges, discounts, net — discounts are never netted invisibly into prices. |

## 4. Workflow

Automatic batches: eligibility run → WF-04 batch approval (P2). Manual grants: WF-04 threshold ladder (P4). Scholarships: nomination → committee (P3/P5) → award. Waivers: WF-06. Renewal queue at rollover: per-instance decisions. Revocation: P2. All effects atomic with final approval (BR-WF-009).

## 5. User roles

Finance Manager (owner), Registrar (sibling data source), HR (staff-link source), Principal / Owner-Board role (threshold approvals, envelopes), Scholarship Committee (decisions), Cashier (view net positions only), Auditor (register), Parent (portal: sees own applied discounts on statement — transparency; hardship details never surfaced).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Configure types/policies | Finance Manager + Principal (P2) |
| Run automatic batches | Finance Manager |
| Propose manual grants | Finance staff |
| Approve per threshold | WF-04 ladder (FM / Principal / Owner) |
| Scholarship programs & envelopes | Principal + Owner |
| Committee decisions | Committee members |
| Waivers | WF-06 chain |
| Revoke | FM + Principal |
| View hardship docs 🔒 | FM, Principal only |

## 7. Database concept

Entities: `DiscountType` (policy fields, stacking, renewal mode); `EligibilityRule` (automatic logic config: sibling ladder, staff link); `DiscountGrant` (student-year × type, basis value, source: auto/manual/scholarship, workflow state, validity, sponsor-note); `ScholarshipProgram` + `Envelope` (caps, consumption); `Waiver`; `RenewalQueueItem`; application lines live as discount documents against charges (Module 19 model). Sibling computation reads ParentStudentLink (Module 11) — single family truth. |

## 8. Required screens

1. Type & policy catalog — stacking matrix visualization, caps.
2. Automatic run console — eligibility diff (new/changed/lost), batch approval submission.
3. **Grant desk** — student/family position with gross/net preview per proposed discount, threshold route display, document upload (hardship 🔒), chain submission.
4. Scholarship center — programs, envelopes with consumption bars, nomination lists, committee decision screen, award letters (Module 18 engine).
5. Renewal queue (rollover season) — instance list with last-year context, bulk decisions.
6. Waiver flow — against specific late/misc charges.
7. Registers — discount register, waiver register, revocations (auditor views).
8. Portal: statement shows discount lines (type label only, no internal reasons).

## 9. Validation rules

Stacking caps enforced at grant (combined preview); envelope consumption hard-blocks over-budget awards (Owner override T1); sibling recompute previews family delta before batch; grant validity within year; basis values within type bounds; revocation effective date ≥ today; waiver ≤ target charge remainder; hardship docs required for hardship type; net position never negative (BR-DIS-005). |

## 10. Reports

**Discount register** (by type/approver/period — the owner report) · Revenue impact summary (gross vs discounts vs net, by grade/type, year-over-year) · Sibling discount audit (family structures vs applied ladders) · Staff discount vs employment status reconciliation · Scholarship envelope consumption & outcomes · Waiver register · Renewal decisions register · Stacking exceptions report · Threshold-approval analysis (who approves what volumes — governance signal). |

## 11. Dashboard widgets

Finance Manager: total discounts YTD vs budget %, pending grant approvals, renewal queue depth (season). Principal/Owner: revenue given away by type (donut), scholarship envelope status. Auditor: T1 discount changes feed.

## 12. Notifications

`DiscountApproved/Applied` → payer (statement update), FM; `DiscountRejected` → proposer with reason; `EligibilityChanged` (sibling/staff delta) → FM queue; `EnvelopeThreshold` (80%) → Principal; `RenewalQueueOpen` → FM (rollover trigger); `DiscountRevoked` → payer (formal), FM. |

## 13. Future enhancements

Sponsor-funded scholarship billing (activates with BR-FEE-004: sponsor pays the discounted portion — becomes a payer transaction, not revenue loss); early-payment dynamic discounts (pay-annual-by-date engine with Module 20); discount simulation (what-if revenue modeling for management); marketing promo codes for admissions season (careful governance).

## 14. Open questions

1. Sibling discount as **discount** (proposed — visible give-away governance) vs **structure pricing** (Module 19 Q/Future): confirm the discount approach with pilot owner (most Gulf schools present it as discount). |
2. Sibling ladder recompute on mid-year withdrawal: keep-for-year (proposed default) vs adjust-forward — confirm policy norm. |
3. Owner/Board approval role: is a real board approval loop needed (offline minutes + system record) or in-system role sufficient? Proposed: in-system Owner role. |
4. Early-payment discount in v1 (simple: % if paid-in-full by date) — cheap to include via type + condition; **recommend including**; confirm. |
