# Module 26 — Library

**Phase:** 7 — Student services | **Status:** Draft for review | **Rule prefix:** `BR-LIB`

---

## 1. Purpose

A school-grade library system: cataloging (bilingual, Arabic-capable classification), copy-level inventory, circulation (borrow/return/renew/reserve) for students and staff, fines/lost-item charges through the finance engine, reading-program support, and clearance integration (withdrawal/offboarding checklists).

## 2. Scope

**In:** catalog (titles, authors, subjects, MARC-lite fields, Dewey/local classification), copy management (barcoded), member policies per role/stage (limits, durations), circulation desk, reservations, overdue/fine policy (fines optional per school), lost/damaged handling (replacement charge via Module 19 misc), inventory counts (stocktake), reading programs (class visits, reading logs light), digital-resource links (URL-level v1).
**Out:** full digital library/e-book DRM (Future), procurement/acquisitions budgeting (light want-list only v1), inter-library loan (Future).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-LIB-001 | **Catalog:** title records bilingual-capable (Arabic originals with transliteration fields), classification (Dewey default + local scheme option), subjects/tags, age/stage suitability flag; copies are barcoded units with status (`Available / Loaned / Reserved / Repair / Lost / Withdrawn`) and acquisition info (cost — drives replacement charges). |
| BR-LIB-002 | **Members:** students and staff auto-membered from their records (no separate registry, BR-GLB-002); policy per member class & stage: max concurrent loans, loan days, renewals, reservation limits; guest/parent membership off by default (config). |
| BR-LIB-003 | **Circulation:** checkout requires available copy + member within limits + no blocking flags (over-limit fines, clearance hold); due dates respect the calendar (BR-CAL — due dates skip holidays); renewals within policy unless reserved by another member; returns update status instantly; every event logged (member, copy, actor, time). |
| BR-LIB-004 | **Reservations:** queue per title; available-copy hold window (e.g., 2 days) then passes on (BR-ADM-006 offer pattern); notification-driven. |
| BR-LIB-005 | **Overdues & fines:** overdue notices per ladder (BR-NOT digest-friendly); fines optional per school (config): per-day rate with cap → posted as misc charges (Module 19) per batch-confirm (BR-FEE-007 pattern — librarian proposes, finance-visible); schools disabling fines rely on notices + clearance holds. |
| BR-LIB-006 | **Lost/damaged:** declared lost (by member or after N overdue days per policy) → replacement charge (copy cost or fixed policy price) via Module 19 misc + copy → Lost; found-later flow reverses per finance rules (credit note if charged). |
| BR-LIB-007 | **Clearance:** WF-03 withdrawal / BR-EMP-008 offboarding checklists include library item (open loans + unpaid library charges block clearance per the parallel-checklist model). |
| BR-LIB-008 | **Stocktake:** periodic inventory sessions (scan-based) produce discrepancy lists (missing/misplaced) with librarian resolution actions; catalog is never bulk-corrected outside a stocktake session (audit clarity, T2). |
| BR-LIB-009 | Class-visit mode: batch checkout for a section during library periods (roster-based fast issue) — the school-library reality of 25 children in 10 minutes. |

## 4. Workflow

Circulation is P1 (desk speed). Fine batches: librarian proposal → confirm (posts charges). Lost-item charge: P2 (librarian → finance visibility). Stocktake: open session → scan → resolve → close (P2 sign-off). Withdrawn-copy disposal: P2.

## 5. User roles

Librarian (owner), Library Assistant (circulation), Teachers (class-visit coordination, view), Students/Parents (portal: search, my loans, reservations), Finance (charge visibility), Registrar/HR (clearance consumers).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Catalog management | Librarian |
| Circulation desk | Librarian, Assistant |
| Fine/lost charge proposals | Librarian (finance-confirmed) |
| Policy configuration | Librarian + Sys Admin |
| Stocktake | Librarian (+P2 close) |
| OPAC search / my loans | All members (portal) |

## 7. Database concept

Entities: `Title` (biblio fields, classification), `Copy` (barcode, status, cost), `MemberPolicy` (class × stage rules), `Loan` (member × copy, dates, renewals), `Reservation` (queue), `FineProposal`/charge refs (Module 19), `StocktakeSession` + lines, `ReadingLog` (light, class programs). Members resolve to Student/Employee refs directly. |

## 8. Required screens

1. Catalog manager — title entry (bilingual fields, cover image), copy registration (barcode print), import (ISBN lookup Future; CSV v1).
2. **Circulation desk** — scan-first UX: member scan/search → loans panel → copy scan checkout/return; blocking flags surfaced; class-visit batch mode (roster grid). |
3. Reservations board — queues, holds expiring.
4. Overdue & fines console — ladder status, fine batch proposal.
5. Stocktake wizard — session, scan capture, discrepancy resolution.
6. OPAC (portal) — search (Arabic-aware), availability, my loans/renewals, reservation.
7. Reading programs — class visit scheduler (timetable-aware), reading logs per section.

## 9. Validation rules

Checkout blocked over limits/flags (override: librarian permission, logged); due-date calendar shifts; renewal blocked when reserved; return of Lost copy triggers found-flow; stocktake close requires all discrepancies resolved/acknowledged; replacement charge requires copy cost or policy price; barcode uniqueness. |

## 10. Reports

Collection statistics (titles/copies by class, age) · Circulation activity (loans per period/member class/grade — reading-culture metric) · Top titles & never-circulated list (weeding aid) · Overdue & fines register · Lost/damaged register with charge status · Stocktake discrepancy reports · Clearance-pending list · Class reading program participation. |

## 11. Dashboard widgets

Librarian: loans today, overdues count, holds to pull (reservation fulfillment), stocktake progress. Principal: circulation trend, reading program reach. Portal: my due-soon items, reservation status.

## 12. Notifications

`LoanDueSoon` (D-2) → member/parent; `ItemOverdue` (ladder, digest) → member/parent; `ReservationAvailable` (hold window) → member; `FinePosted` → payer; `LostItemCharged` → payer; `ClearanceHold` → Registrar/HR (checklist feed). |

## 13. Future enhancements

ISBN/Z39.50 cataloging lookup; e-book platform integration (single sign-on to providers); RFID circulation & security gates; acquisitions & budget module; reading-level analytics (leveled-reading programs); inter-campus catalog union (multi-school future).

## 14. Open questions

1. Fines-enabled default: off (proposed — many Gulf schools avoid child fines; clearance holds suffice) — confirm. |
2. Arabic classification scheme support level: Dewey-with-Arabic-labels (proposed v1) vs full local schemes — confirm librarian expectations. |
3. Parent borrowing (family library) — off by default; any pilot demand? |
4. Textbook distribution: handled in Store (Module 28 book bundles) not Library — confirm this boundary (some schools run textbooks through the library). |
