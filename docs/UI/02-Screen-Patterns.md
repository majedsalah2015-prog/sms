# UI 02 — Screen Patterns, Forms & Keyboard Standards

**Phase:** 11 | **Status:** Draft for review | **Owner:** UI/UX Architect

> The pattern library: every module screen names its pattern; new patterns require this doc amended first (mirrors catalog change control).

---

## 1. Pattern catalog

| # | Pattern | Used by (examples) | Anatomy |
|---|---------|--------------------|---------|
| P-LIST | **List/Register** | students, charges, incidents, catalog screens | Filter bar + chips → paged grid (sortable, column chooser, saved views) → row actions + bulk-action bar; export button permission-gated (BR-SEC-021); footer totals where financial. |
| P-DETAIL | **Record detail (tabbed)** | student file, parent file, employee file, program | Identity header (photo, number, status chip, key facts, alert badges 🔒-aware) + tab set (UI-01 §4) + history panel access. |
| P-WIZARD | **Multi-step wizard** | setup, registration, rollover steps, withdrawal, stocktake, merge | Stepper with per-step validation, save-and-resume, review-before-commit summary step, atomic commit (BR-WF-009). |
| P-BOARD | **Kanban/status board** | admissions pipeline, case board, PDC lifecycle | Columns = workflow states (doc 05 vocabulary), cards with SLA aging, drag = transition (permission + reason prompts per rules). |
| P-SHEET | **Grid capture sheet** | attendance, marksheets, staff attendance, screening results | Roster rows × input columns; keyboard-first (§3); autosave draft; single-submit; completeness meter; photo column optional. |
| P-POS | **Point of sale** | cashier, cafeteria, store | Left: identity + context panel (position/balance/warnings); center: item tiles or amount pad; right: basket/allocation preview; huge touch targets; offline indicator; receipt print flow. |
| P-CAL | **Calendar/timetable grid** | calendar board, timetable builder, exam schedule, leave calendar | Time × resource matrix, drag placement with live validation panel, conflict list dock. |
| P-CONSOLE | **Operations console** | daily cover, attendance monitor, delivery ops, trip board | Live tiles + exception queues; auto-refresh (L class); every exception row carries its one-click resolution action. |
| P-INBOX | **Approvals/threads inbox** | My Approvals, messaging threads, review queues | List + preview pane; inline approve/reject/return with reason; SLA badges; keyboard triage (§3). |
| P-CONFIG | **Configuration matrix** | subscriptions matrix, permission tree, communication matrix, stacking matrix | Rows × toggles/values grid with product-floor indicators (locked cells + explanation tooltips). |
| P-DASH | Dashboard | per Phase 9 specs | Widget grid per Module 31 rules. |
| P-STMT | **Statement/position** | student/payer statement, wallet history | Header identity + running-balance table (multi-year, as-of picker) + document drill links + print (Off layout). |
| P-LAUNCH | **Department launcher** | the landing page after sign-in, and one page per department | Grid of large icon tiles, one per department a school is staffed as — finance, students, secretariat, teaching staff, reports, timetable, cover rota. A tile shows only if the signed-in user can open at least one screen behind it (BR-SEC-010) and its module's feature is on (BR-SET-006); a department of exactly one screen opens that screen instead of a page holding one card. Second level is the same grid at card size, plus the embedded ERP's own groups under finance. See `Sms.Web/Navigation/WorkspaceCatalog`. |

**Why a second taxonomy.** The sidebar groups screens the way the product is *built* — the 36 modules of `docs/Modules`, in the stages they were built in. P-LAUNCH groups the same screens the way the school is *staffed*. The two overlap deliberately: a cashier does not think of Fees, Instalments, Payments and Discounts as four modules, and parent records belong to both the students desk and the secretariat because two people reach them from two directions. Forcing a partition would make one of the two audiences wrong.

Every launcher link names the same `(module, screen)` pair as the `[RequirePermission]` attribute on the action it opens, through the same constants — so what the launcher offers and what the server allows cannot drift, and a rename is a compile error rather than a dead tile. `Sms.Web.Tests/WorkspaceCatalogTests` holds that by reflection.

## 2. Forms & validation

| Rule | Detail |
|------|--------|
| Layout | 2-column desktop, 1-column < md; labels above fields; bilingual pairs per UI-01 §7; section headings every ≤ 8 fields. |
| Required | Asterisk + summary; blocking errors inline under field **and** in a top error summary (links focus the field); warnings amber with override affordance only when permission-held (BR-GLB-111) — override always demands the reason inline. |
| Server truth | All validation server-enforced (BR-GLB-110); client mirrors for speed only; double-submit guarded (idempotency keys on posting forms — receipts, registrations). |
| Pickers not text | Referential fields always pickers (BR-GLB-112); free text only for genuinely free content. |
| Dates | Single date component product-wide: Gregorian input with Hijri sub-display when school config enables (ADR-4); range pickers for periods; no raw text dates. |
| Money | Right-aligned (LTR digits even in RTL — doc 03 §4), currency affix per school, thousands separators, negative in red parentheses on statements. |
| Autosave | P-SHEET and long wizards autosave drafts (BR-GLB-031 drafts harmless); explicit Submit remains the state transition. |
| Dirty guard | Unsaved-changes prompt on any navigation; per-tab dirty dots (UI-01 §4). |

## 3. Keyboard shortcuts

**Global (staff app):**

| Keys | Action |
|------|--------|
| `/` or `Ctrl+K` | Focus global search |
| `Ctrl+S` | Save current form (where a primary save exists) |
| `Alt+A` | My Approvals |
| `Alt+N` | Notification center |
| `Alt+Y` | Year switcher |
| `Esc` | Close modal / clear picker |
| `?` | Shortcut cheat-sheet overlay |

**Grid/sheet (P-SHEET, P-LIST):** arrow-key cell navigation; `Enter` = next row same column (marksheet entry rhythm); `Space` toggles status cells (attendance one-key statuses: `P/L/A/E…` typed directly); `Ctrl+Enter` submit sheet; `Ctrl+D` copy-down (with audit note where marks — flagged, not silent).

**Inbox (P-INBOX):** `J/K` next/prev, `A` approve, `R` return, `X` reject (each opening the reason panel per rules — keys accelerate, never skip governance).

**POS (P-POS):** numeric keypad-first; `F2` tender, `F4` print/finish, barcode scan = keyboard wedge input (focus-trap design so scans never land in wrong fields).

RTL note: arrow-key semantics follow visual direction (left-arrow moves visually left in both directions — doc 03 §5).

## 4. Tables & grids

Column headers sortable (server-side beyond page threshold); sticky header + sticky identity column (name/number) on wide grids; pagination standard 25/50/100 with URL state; totals row fixed for financial lists; empty states instructive ("No applications match — clear filters / start new application"); loading skeletons not spinners for lists; row-level history icon where audited (BR-AUD-008).

## 5. Print & document output

Two print classes: **operational prints** (rosters, manifests, duty sheets — browser print CSS, A4, header with school + context + printed-by/at) and **official documents** (Off class — certificates, receipts, report cards, statements: server-rendered PDF per Phase 9 T-5 engine, template slots per BR-CRT-002, bilingual layouts, QR blocks). Every P-LIST offering print gets a print stylesheet — no screenshots-as-reports.

## 6. Error & empty philosophy

Errors say what happened, what it means, what to do — bilingual, no codes-only (BR-GLB-111); permission-absence = not-found for portal (BR-SEC-010), styled access-note for staff deep links; offline states (POS, trip console, capture sheet) show queued-count + sync status explicitly.

## 7. Open questions

1. Attendance one-key status letters: localize per language (ح/ت/غ…) or fixed Latin? **Recommend both accepted** (layout-aware). |
2. Copy-down in marksheets (efficiency vs error risk): keep with audit-flag (proposed) or remove? Confirm with pilot teachers. |
3. Saved-view sharing (user → role-level shared views) in v1? Proposed: personal only, sharing v1.x. |
