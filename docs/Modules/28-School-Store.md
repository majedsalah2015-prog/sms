# Module 28 — School Store

**Phase:** 7 — Student services | **Status:** Draft for review | **Rule prefix:** `BR-STO`

---

## 1. Purpose

Sell and distribute school goods — uniforms, textbooks/book bundles, stationery, spirit items — with real inventory (variants and sizes), grade-based book/uniform bundles that can charge to the student fee account, POS and charge-to-account sales, and clean finance integration.

## 2. Scope

**In:** item catalog with **variants** (size/color for uniforms), price lists (versioned), inventory (receive, adjust, stocktake, low-stock), **bundles** (per-grade book sets, uniform kits) with fee-account charging (Module 19 service-linked book/uniform categories), POS sales (cash/wallet/charge-to-account per config), returns/exchanges (uniform size swaps), distribution mode (bundle handout sessions with per-student checklist), reservations/pre-orders (season), staff sales.
**Out:** supplier procurement ledger (receive-only v1, like BR-CAF-006), manufacturing/embroidery job tracking (out), e-commerce storefront (portal pre-order only v1).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-STO-001 | **Catalog:** items bilingual with category (uniform/book/stationery/other), VAT class per category (BR-FEE-001 mapping — uniforms/books may differ from tuition treatment per regime), variant matrix (size/color) with per-variant SKU/barcode and stock; price lists versioned (T2). |
| BR-STO-002 | **Bundles:** named kits per grade-year (Book Bundle G5, Uniform Kit Boys G1: item+variant-agnostic lines resolved at handout) with bundle price (≠ sum allowed); bundle assignment can be: charged at registration (auto with BR-FEE-003 service categories), opt-in (portal/counter), or handout-time charge — per school config. |
| BR-STO-003 | **Sales channels:** POS walk-in (cash / cafeteria-wallet tender per config / card via Module 21 methods) and **charge-to-account** (posts a Module 19 charge to the payer — permission- and cap-gated per school: e.g., allowed for uniforms up to X, disabled for stationery). All sales strict-receipted or account-charged — no undocumented outflow. |
| BR-STO-004 | **Distribution sessions:** for auto-charged bundles, handout is tracked per student (checklist: received items, sizes chosen, signature/e-ack); undistributed items visible until resolved (leakage control — paid-but-not-received is a real complaint source). |
| BR-STO-005 | **Returns/exchanges:** size exchanges free within window (stock swap, logged); returns per category policy (books sealed-only etc.) → refund via WF-05 or account credit note (Module 19/21 rules); return window and condition rules per category. |
| BR-STO-006 | **Inventory:** perpetual stock per variant (receive/sell/adjust/waste/count); negative stock blocked (override logged); stocktake sessions per BR-LIB-008 pattern; low-stock thresholds per variant drive reorder report (no PO module v1 — want-list export). |
| BR-STO-007 | Clearance integration: unpaid store account-charges enter finance clearance (WF-03/offboarding) automatically (they're normal charges); undistributed-paid bundles resolve at withdrawal (deliver or credit). |
| BR-STO-008 | Sales voids/returns follow BR-PAY session rules; price overrides at POS prohibited (manager reprice only, T1) — product stance against discount-at-till leakage; formal discounts only via Module 22. |

## 4. Workflow

POS: P1. Charge-to-account: P1 within caps (beyond cap → P2 Finance). Bundle season: define → assign/charge batch (preview totals, BR-FEE batch pattern) → distribution sessions → completion report. Returns: policy-gated desk flow (refund path via WF-05 when money returns). Stocktake: session → close (P2).

## 5. User roles

Storekeeper (owner), Store Operators, Finance Manager (charge caps, reconciliation), Registrar (bundle-grade coordination), Parents (portal: pre-orders, bundle status, receipts), Sys Admin (config).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Catalog/prices/bundles | Storekeeper (+FM for price publish P2) |
| POS & distribution | Operators |
| Charge-to-account beyond cap | FM approval (P2) |
| Returns/exchanges | Operators (policy), refunds via WF-05 chain |
| Inventory receive/adjust | Storekeeper (adjust T1) |
| Reprice | Storekeeper manager permission (T1) |
| Stocktake close | Storekeeper + P2 |

## 7. Database concept

Entities: `StoreItem` + `Variant` (SKU, barcode, stock), `PriceList` (versions), `Bundle` + `BundleLine` (grade-year linkage), `StoreSale` (+ lines, tender: cash/wallet/account-charge ref), `DistributionSession` + `HandoutRecord` (per student checklist, ack), `ReturnExchange`, `StockMovement`, `PreOrder`. Account-charges and refunds are Module 19/21 documents (single money truth). |

## 8. Required screens

1. Catalog & variant manager — matrix editor (sizes × colors), barcode printing.
2. Bundle designer — per grade-year, price vs component sum display, assignment mode config.
3. **POS** — barcode-first, variant picker, tender selection incl. account-charge (position glimpse + cap check), receipt print.
4. **Distribution console** — session per grade/section: student checklist with size capture, ack collection, progress bar; undistributed follow-up list.
5. Inventory console — receive, adjustments, stocktake, low-stock board.
6. Returns desk — policy prompts, exchange fast-path.
7. Portal: bundle status per child (charged/distributed), pre-order form (season), receipts.

## 9. Validation rules

Variant required where matrix exists; stock guards; account-charge within cap + payer position visibility; bundle batch preview confirmation; handout only against charged/paid status per config (school choice: distribute-then-collect vs pay-first); return window/condition enforcement; price changes only via versioned lists. |

## 10. Reports

Sales by category/tender/period · Bundle completion (charged vs distributed vs pending — the season report) · Inventory valuation & movements · Low-stock/reorder want-list · Returns/exchanges register · Account-charge register (with finance aging tie-in) · Shrinkage report (stocktake variances) · Uniform size-demand analysis (procurement planning aid). |

## 11. Dashboard widgets

Storekeeper: today's sales, low stock count, distribution progress (season), pending returns. FM: account-charges this month, store cash to reconcile. Portal: bundle ready-for-pickup notice.

## 12. Notifications

`BundleCharged` → payer (with contents); `BundleReadyForPickup` / `DistributionSessionScheduled` → parents; `HandoutCompleted` → payer (ack copy); `PreOrderReady` → parent; `LowStock` → Storekeeper; `AccountChargeCapExceeded` → FM. |

## 13. Future enhancements

Supplier & PO module; e-commerce storefront with gateway; embroidery/customization job tracking; RFID inventory; demand forecasting from enrollment pipeline (Admissions feed); merit-points redemption store (Module 25 tie-in).

## 14. Open questions

1. Textbook boundary confirmed at Library Q4: **Store owns textbook distribution** (bundles), Library owns lending collections — confirm. |
2. Distribute-then-collect vs pay-first default (BR-STO handout gate): proposed pay-first (or charged-to-account) — confirm market practice. |
3. Cafeteria-wallet as store tender (one student money pot) — proposed yes (config); confirm parents' mental model tolerance. |
4. Uniform vendor consignment (school sells vendor stock) — any pilot need? v1 assumes school-owned stock. |
