# UI 01 — Foundations: Shell, Navigation, Design Language

**Phase:** 11 | **Status:** Draft for review | **Owner:** UI/UX Architect

> Stack: ASP.NET Core MVC + Bootstrap 5 (native RTL) + thin jQuery (T-4). Two applications, one design language: **Staff app** (dense, keyboard-first, desktop-primary) and **Portal** (parent/student: simple, mobile-first).

---

## 1. Design principles

1. **Speed for daily screens:** attendance capture, cashier, POS, marksheets are measured in seconds (NF targets) — big targets, keyboard flows, minimal navigation depth.
2. **Bilingual parity:** Arabic UI is a first-class layout, not a mirrored afterthought (doc 03 of this guide); every screen is reviewed in both directions before acceptance.
3. **Deny-by-default rendering:** users see only what they may do (BR-GLB-070) — no disabled menus for missing permissions; absent means invisible.
4. **Context always visible:** school (multi-school future), working academic year (BR-AYR-010), and environment badge (demo/staging) live permanently in the shell header.
5. **One pattern per problem:** every module reuses the pattern library (doc 02) — a Registrar who learns one list screen has learned them all.
6. **Trust surfaces:** every record shows created/modified (BR-GLB-007) inline and one-click history (BR-AUD-008); numbers on dashboards equal their drill-downs (BR-DSH-002).

## 2. Application shell (staff)

```
┌──────────────────────────────────────────────────────────────┐
│ Logo | School name | [Working Year ▾] | Global search | 🔔 | 👤│  ← header (fixed)
├──────────┬───────────────────────────────────────────────────┤
│ Sidebar  │  Breadcrumb  ·  Page title  ·  page actions       │
│ (module  │ ─────────────────────────────────────────────────│
│  nav,    │                                                   │
│ collaps- │              Content area                         │
│  ible)   │                                                   │
└──────────┴───────────────────────────────────────────────────┘
```

- **Header:** year switcher (permission-scoped; non-active year turns the header amber — the BR-AYR-010 warning), global search (§5), notification bell (Module 33 center), approvals badge (doc 05 inbox count), profile menu (language toggle, preferences, sign-out).
- **Sidebar:** module groups mirroring the six phases' clusters; collapsible to icons; permission-filtered; max 2 levels (module → screen); a "pinned" section per user (self-service favorites).
- **Portal shell:** top navbar only (no sidebar), children switcher for parents, bottom tab bar on mobile (Home / Children / Fees / Messages / More).

## 3. Navigation & menu rules

| Rule | Detail |
|------|--------|
| Depth | Sidebar ≤ 2 levels; anything deeper is in-page tabs (§4) — never fly-out menus. |
| Grouping | Product-fixed menu taxonomy (consistency across customers); feature toggles (BR-SET-006) remove groups wholesale. |
| Breadcrumbs | Always: Module / Screen / Record identity (e.g., Students / Student File / STU-26-00042 أحمد). Record crumbs use the official number + name. |
| Deep links | Every screen and record has a stable URL (permission-checked); notification/dashboard drills land on the exact tab/row. |
| Back behavior | Browser back always safe (no state loss on lists: filters/paging live in the URL). |
| My Approvals | Reachable from every page via the header badge — the doc 05 unified inbox is the shell's second home. |

## 4. Tabs (in-page)

- Detail screens use horizontal tabs (Student File's 16 tabs is the maximal case) with overflow-to-dropdown after 8 visible; tab set is permission-filtered (BR-STU-004 pattern); active tab in URL (deep-linkable).
- Restricted tabs (🔒 medical/behavior/finance) render a lock-context banner (why this user sees it — role name) as a subtle audit-awareness cue (T0 realities, doc 07).
- Tabs never lazy-lose data: switching tabs preserves unsaved edits with a dirty-state dot on the tab and a guard on navigation.

## 5. Search standards

| Search | Behavior |
|--------|----------|
| **Global search** (header) | One box: matches students/parents/employees by name (Ar or En, diacritic-insensitive), any official number (BR-NUM-006 — receipt, certificate, incident…), and phone. Results grouped by type, permission-filtered, keyboard-navigable. Target: result list < 500 ms. |
| **List screens** | Instant filter box (client-side ≤ 200 rows, server-side beyond) + structured filter bar: the standard dimensions (year — usually locked to working year, grade, section, status, date range) as compact controls; saved filter sets per user; active filters shown as removable chips. |
| **Pickers** | Person/entity pickers are type-ahead (min 2 chars, both languages, shows disambiguating context: section for students, children for parents); recently-used items first. |
| Arabic matching | Normalized matching: hamza/alef variants (أ/إ/آ/ا), ta-marbuta/ha (ة/ه), ya/alef-maqsura (ي/ى) treated as equivalent in search (collation + normalization layer). |

## 6. Design tokens (Bootstrap 5 theme)

- **Palette:** neutral professional base + per-school accent color (branding, BR-SCH-006) applied to header/primary buttons only — school identity without breaking product consistency. Semantic colors fixed product-wide: success/danger/warning/info per Bootstrap defaults tuned for WCAG AA contrast (doc 03).
- **Typography:** Latin: Inter (or system stack); Arabic: **IBM Plex Sans Arabic** (bundled, self-hosted) — chosen for screen legibility + matching weights with Latin. Numerals: Latin digits default in data; Arabic-Indic digits as user preference (BR-NUM-007 display-only).
- **Density:** staff app uses compact tables (Bootstrap `table-sm` baseline) with 40px row height; portal uses comfortable spacing.
- **Iconography:** Bootstrap Icons; every icon paired with text label except universally-learned shell icons (bell, search).
- Status rendering: one product-wide status-chip component (color + label from the workflow state vocabulary, doc 05 §3) — never free-styled per module.

## 7. Language toggle & bilingual entry

- Session language switch in profile menu, per-user default; switch reloads in place (URL-preserved).
- **Bilingual entry pattern:** NameAr + NameEn side-by-side (Ar field right, En field left, regardless of UI direction) with copy-transliterate helper button (assist only, never auto-commit); both mandatory before activation (BR-GLB-001) with paired validation display.
- Mixed-direction data in grids: `dir="auto"` per cell + alignment rules from doc 03.

## 8. Notifications & feedback surfaces

Toast for success (auto-dismiss 4 s), inline alert for errors (persistent until resolved), blocking modal only for destructive confirmations (void, revoke, close-year) which always restate the object identity and require the reason field inline where rules demand it (BR-GLB-111). Long operations (rollover steps, batch generation) use progress panels with per-item results, never spinners without counts.

## 9. Open questions

1. Per-school accent color: full theme freedom vs curated 8-color palette? **Recommend curated palette** (protects contrast compliance). |
2. Dark mode: out of v1 (proposed) — staff environments are daytime; revisit on demand. |
3. Transliteration helper engine choice (assist quality varies) — implementation-time evaluation; UX contract fixed here. |
