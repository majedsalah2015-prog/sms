# Module 21 — Payments

**Phase:** 6 — Finance | **Status:** Draft for review | **Rule prefix:** `BR-PAY`

---

## 1. Purpose

Collect and account for money: cashier sessions, receipts (strict-numbered), payment methods including **post-dated cheques** (Gulf reality), allocation of payments to installments/charges, refunds (WF-05), advances & overpayments, bank deposit reconciliation, and gateway-ready online payment design (activated later per Phase 1 decision Q5).

## 2. Scope

**In:** cashier till sessions (open/close/handover, cash counts), receipt capture (methods: cash, card/POS, bank transfer, cheque, PDC), allocation engine (payment → installments/charges), PDC registry (lodgement, maturity, clearance, bounce), refunds & refund vouchers (WF-05), advances/credit balances, bank reconciliation (deposits vs receipts), day-close controls, gateway integration design (dormant), copy-fee/misc collections (Modules 18/28 hooks).
**Out:** what is owed (Modules 19/20), discount grants (Module 22), payroll-side money (out of product).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-PAY-001 | **Till sessions:** every counter receipt belongs to an open cashier session (cashier × till × day); sessions open with float declaration and close with cash count vs system total — variances recorded with reason and P2 (Finance Manager) acknowledgment; no backdated receipts into closed sessions (corrections = reversal + new receipt). |
| BR-PAY-002 | **Receipts** (strict series, BR-NUM): payer-addressed (BR-FEE-004), method-detailed (card ref, transfer ref, cheque no/bank/date), bilingual print with VAT-compliant layout; issued atomically with posting (BR-NUM-003); voiding a receipt (same-day, pre-day-close, P2) keeps the number Void with reason; after day-close only reversal documents. |
| BR-PAY-003 | **Allocation:** every payment allocates to installments/charges — default auto-allocation oldest-due-first per payer (config: per-child targeting allowed at capture); allocations are re-openable only via reversal (audit-clean); unallocated remainder becomes **advance/credit balance** on the payer (visible on statements; auto-consumed by next due unless held per request). |
| BR-PAY-004 | **PDC registry:** post-dated cheques lodge with full detail (bank, number, date, amount, covered installments per BR-INS-009); lifecycle `Lodged → Due → Deposited → Cleared / Bounced → Replaced/Settled`; clearance converts to a receipt (numbered at clearance date); **bounce** raises: installment un-covers, dunning resumes at escalated step, bounce-fee charge per policy (Module 19 misc), incident flag on payer (repeat-bounce report), P2-notified. PDC totals are exposure-reported, never counted as income. |
| BR-PAY-005 | **Refunds (WF-05, P3):** against credit balances or refundable settlements (BR-FEE-006 withdrawal calc); refund voucher (strict series) with method (cash from till / transfer); refund never exceeds refundable position (hard); reasons + approvals per chain; refunds to the original payer only (anti-fraud default; exception = Principal T1). |
| BR-PAY-006 | **Day close:** per school day per till + a finance-level daily close: totals by method, variance sign-offs, deposit slip preparation (cash/cheques to bank); **bank reconciliation** matches deposits and transfer receipts to bank statement lines (manual match v1, import-assisted; statement import format per bank pack). |
| BR-PAY-007 | **Online gateway (design-ready, dormant):** portal pay-now creates a pending payment intent → gateway callback posts receipt automatically (numbered, method=gateway, fee handling per config) → allocation per BR-PAY-003; reconciliation via gateway settlement reports. No gateway code in v1 scope; the design fixes the model so activation is additive (Phase 1 Q5). |
| BR-PAY-008 | All money documents T1; till sessions and closes system-audited; cashier permission ≠ refund permission ≠ void permission (segregation, BR-SEC principle 3). |

## 4. Workflow

Receipt: capture (P1, session-gated). Void: P2 same-day. Refund: WF-05 (P3: Officer → FM; > threshold + Principal P4). PDC bounce handling: auto-flags + officer follow-up case. Day close: cashier close → FM daily close (P2 acknowledgment of variances). Gateway callback: automated posting path (dormant).

## 5. User roles

Cashier (capture, own session), Collection Officer (PDC follow-up, allocations review), Finance Manager (closes, voids approval, refund chain, reconciliation), Principal (thresholds), Parent (portal: receipts view, pay-now future), Auditor (registers).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Open/close own till | Cashier |
| Capture receipts | Cashier (session), Officer |
| Void (same-day) | Cashier request → FM approve (P2) |
| Manage PDC registry | Officer, FM |
| Approve refunds | WF-05 chain |
| Re-allocation (via reversal) | FM |
| Daily close / reconciliation | FM |
| View registers | Finance, Auditor; Parent (own receipts) |

## 7. Database concept

Entities: `TillSession` (float, counts, variance); `Receipt` (strict no., payer, method details, status); `PaymentAllocation` (receipt ↔ installment/charge lines); `AdvanceBalance` (derived-materialized per payer); `PDC` (lifecycle states, covered-installment links); `RefundVoucher` (strict no., WF-05); `DayClose` (till + finance levels); `BankStatementLine` + `ReconciliationMatch`; `PaymentIntent` (gateway-dormant). Position math (BR-FEE-008) reads allocations — single payment truth. |

## 8. Required screens

1. **Cashier screen** — the speed-critical screen: payer/student search → position summary → amount + method capture (PDC sub-form) → allocation preview (auto, adjustable) → print receipt; big-button UX, keyboard-first (Phase 11 priority with attendance sheet). |
2. Till session console — open/float, live totals by method, close/count wizard with variance capture.
3. PDC registry — lifecycle board (due this week, deposited, bounced), bulk deposit action, bounce handling flow.
4. Refund desk — position check, voucher chain, payout confirmation.
5. Allocation explorer — payer allocation history, reversal flow (FM).
6. Day close & reconciliation — daily totals, deposit slips, bank statement matching workbench.
7. Portal: receipts/statement per family; pay-now placeholder (gateway dormant).

## 9. Validation rules

Receipt requires open session (counter methods); method details mandatory per method (cheque/transfer refs); allocation total = receipt total; PDC date > today at lodgement; refund ≤ refundable position with source documents linked; void only same-day pre-close with reason; day-close blocked with unallocated gateway callbacks or unfinished sessions; reconciliation match totals must balance before period lock. |

## 10. Reports

Daily collection report (by till/method — the treasurer's daily) · Receipt register (strict continuity per BR-NUM auditor report) · PDC exposure & maturity ladder · Bounce register (by payer, repeat offenders) · Refund register with chains · Advance balances list · Deposit & reconciliation status · Cashier variance history · Collection vs expected (Module 20 calendar overlay) · VAT-relevant collections summary (pack-dependent cash-basis views). |

## 11. Dashboard widgets

FM: today's collections by method (live), unreconciled days count, PDCs due this week, pending refunds. Cashier: my session total, receipts count. Principal: month collection vs target. Portal: last receipt, credit balance if any.

## 12. Notifications

`PaymentReceived` (receipt) → payer (with PDF); `PDCDueForDeposit` (D-2) → Officer; `PDCBounced` → payer (formal) + FM + Officer 🔒; `RefundProcessed` → payer; `TillVarianceRecorded` → FM; `DayCloseCompleted` → FM (+ anomalies); `GatewayPaymentPosted` (future) → payer + FM feed. |

## 13. Future enhancements

Gateway activation (regional PSPs: e.g., PayTabs/HyperPay/Telr class — selection per market), card-on-file auto-pay (Module 20 mandate), cash-deposit machine integration, e-wallet channels, SADAD-style national bill presentment (KSA) — high sales value in target market; instant payment notifications via bank APIs.

## 14. Open questions

1. PDC handling confirmed as v1 (strongly recommended for Gulf) — validate cheque prevalence in final country list; if UAE/KSA confirmed, also confirm bounce-fee legality/amounts per pack. |
2. Receipt VAT layout: is a simplified tax invoice per receipt required, or is the charge document the tax invoice and receipt merely settlement? **Per-regime decision (ZATCA: charges are tax invoices)** — needed for template design in Phase 9/11. |
3. Multi-till/branch cash handover chain (cashier→head cashier→bank) depth: v1 proposed two-level (till→finance close) — enough? |
4. Partial-payment receipt against a specific child vs family pool default: proposed family-pool auto-oldest-first with per-child override at capture — confirm cashier practice. |
