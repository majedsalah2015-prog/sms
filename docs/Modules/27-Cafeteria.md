# Module 27 — Cafeteria

**Phase:** 7 — Student services | **Status:** Draft for review | **Rule prefix:** `BR-CAF`

---

## 1. Purpose

Cashless-capable cafeteria operations: student wallet accounts funded by parents, fast POS sales with allergy awareness and parent-set spending controls, menu & item management with light stock, meal-plan subscriptions, and full reconciliation into the finance engine.

## 2. Scope

**In:** item catalog (bilingual, priced, allergen-tagged, nutrition-class tags), menus (daily/weekly), **student wallet** (top-ups, balance, low-balance alerts), parent spend controls (daily limit, item-category blocks), POS sales (wallet, cash), meal-plan subscriptions (prepaid monthly/term plans → Module 19 service charge), light stock (receive/deduct/waste), settlement & reconciliation (Module 21 day-close integration), staff purchases (payroll-deduct flag Future; cash/wallet v1).
**Out:** full inventory/procurement with suppliers (light receive-only v1), kitchen production planning (Future), external catering contracts management (out).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-CAF-001 | **Wallets:** one wallet per student (staff optional); top-ups at cashier (Module 21 receipt, method rules) or portal (gateway-dormant like BR-PAY-007); balances are payer money held — reported as liability, refundable on withdrawal (WF-03 clearance includes wallet settlement); negative balances not allowed (config: small overdraft allowance with cap). |
| BR-CAF-002 | **Spend controls (parent-set, portal):** daily spend limit, blocked item categories; **allergy awareness:** items carry allergen tags; a student with a matching flagged allergy (BR-HLT emergency-banner feed) triggers **POS warning** at sale (v1 warning-level per Module 24 Q5; hard-block config per parent request); control checks run at POS in real time. |
| BR-CAF-003 | **POS sales:** identify student (card/code/name search with photo confirm), basket, wallet-or-cash tender; offline-tolerant queue (sales sync when connectivity returns — lunch rush reality); every sale logged (student, items, tender, operator, till). |
| BR-CAF-004 | **Meal plans:** subscription products (e.g., monthly lunch plan) charged via Module 19 (service-linked category, pro-rated per policy); plan holders redeem per-day entitlement at POS (plan-first tender, wallet fallback); unredeemed-day policy per school (forfeit/credit). |
| BR-CAF-005 | **Menus:** daily/weekly menus published to portal (with allergen and nutrition-class display); item availability follows menu + stock. |
| BR-CAF-006 | **Stock (light):** receive quantities per item, POS auto-deducts, waste/spoilage entries with reason; variance report per stocktake (BR-LIB-008 pattern simplified); no supplier ledger in v1. |
| BR-CAF-007 | **Money integrity:** cafeteria tills follow BR-PAY-001 session rules; wallet top-ups/refunds are Module 21 documents; POS sales settle against wallets internally with daily summary journal into finance (single money truth, BR-FEE-008 alignment); cash sales reconcile in day-close. |
| BR-CAF-008 | **Nutrition policy:** items classify against school/ministry nutrition standards (traffic-light classes per pack); banned-class items cannot enter menus for student sale (staff-only flag exists); reports support ministry canteen compliance where required. |
| BR-CAF-009 | Sales voids same-session P2 (BR-PAY-002 pattern); wallet adjustments only via documented corrections (T1). |

## 4. Workflow

POS: P1 speed path with control checks. Top-up: Module 21 receipt path. Meal-plan subscribe: portal/counter → charge → active. Void: P2. Wallet refund (withdrawal/closure): WF-05 refund flow via clearance. Stocktake: session → resolve → close.

## 5. User roles

Cafeteria Supervisor (owner), POS Operators, Finance Manager (reconciliation, liability), Nurse (allergen data feed — no cafeteria rights), Parents (portal: top-up view, controls, spend history, menus), Students (balance view per stage policy), Sys Admin (catalog config).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Catalog/menu management | Supervisor |
| POS operation | Operators (own till session) |
| Voids | Operator request → Supervisor (P2) |
| Wallet corrections | Finance Manager (T1) |
| Stock receive/waste | Supervisor |
| Spend controls | Parent (own child) |
| Reconciliation | Finance Manager |

## 7. Database concept

Entities: `CafeteriaItem` (allergen tags, nutrition class, price versions), `Menu` (+ lines per day), `Wallet` (student/staff, balance derived from ledger), `WalletLedger` (top-ups from receipts, sales, adjustments, refunds), `Sale` (+ lines, tender, till session ref), `MealPlan` + `Subscription` + `Redemption`, `StockMovement` (receive/deduct/waste/count), `SpendControl` (per student). Wallet balance = ledger sum (never a mutable field). |

## 8. Required screens

1. **POS screen** — photo-confirm student lookup, tile-based items (touch, image tiles for speed), basket, tender (plan/wallet/cash), warning banners (allergy/limit), offline indicator; big-target UX (Phase 11 with cashier screen). |
2. Catalog & menu builder — items with allergen/nutrition tagging, weekly menu grid, publish.
3. Wallet center — balances list, top-up (cashier path), corrections (FM), liability total.
4. Meal-plan manager — products, subscribers, redemption calendar.
5. Stock console — receive, waste, stocktake.
6. Portal: balance & spend history per child, top-up (gateway-dormant), controls editor, weekly menu.

## 9. Validation rules

Sale blocked over wallet balance (unless overdraft config) / over daily limit / blocked category (hard) / allergy per config level (warn default); plan redemption once per entitlement day; menu items must be nutrition-compliant for student sale (BR-CAF-008 hard); top-ups only via numbered receipts; void window session-bound; stock cannot go negative (deduct guard with override log). |

## 10. Reports

Daily sales by till/tender/item · Wallet liability report (finance: total held balances) · Top-up vs spend trends · Meal-plan utilization (redemption %) · Item popularity & waste report · Nutrition compliance report (menu classes, pack format) · Allergy-warning override log 🔒 · Spend-control effectiveness (blocked attempts) · Reconciliation summary (with Module 21 day-close). |

## 11. Dashboard widgets

Supervisor: today's sales live, low-stock items, waste today. FM: wallet liability, unreconciled cafeteria days. Portal (parent): child balance + last purchases, low-balance alert state.

## 12. Notifications

`LowWalletBalance` (threshold) → parent; `TopUpReceived` → parent; `SpendBlocked` (limit/category/allergy) → parent (config, digest); `MealPlanExpiring` → parent; `LargeWalletAdjustment` → FM (T1 alert). |

## 13. Future enhancements

Gateway top-ups (with BR-PAY-007 activation); card/RFID/biometric student identification at POS; pre-order lunch (portal ordering with production counts); full inventory & supplier module; nutrition analytics per student (privacy-reviewed); staff payroll-deduction tender (Module 12 export line).

## 14. Open questions

1. Student identification at POS v1: name+photo search (proposed baseline) — is card-based ID (print student cards with barcode from Module 10 ID feature) expected day one? **Recommend barcode-on-ID-card in v1** (cheap, uses existing card print). |
2. Allergy hard-block default per parent opt-in (proposed) vs school-wide warning-only — confirm with pilot + Module 24 stance. |
3. Cash acceptance at POS: some schools go wallet-only — ship config; default cash-enabled? Proposed yes. |
4. Ministry canteen nutrition standards per pack (KSA/UAE lists) — content needed for BR-CAF-008. |
