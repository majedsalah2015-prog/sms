# UI 03 — RTL, Responsive Design & Accessibility

**Phase:** 11 | **Status:** Draft for review | **Owner:** UI/UX Architect + QA Architect

---

## 1. RTL standards (Arabic is a first-class layout — NF-L2)

### 1.1 Mechanics

| Rule | Detail |
|------|--------|
| Direction switching | `<html dir="rtl" lang="ar">` per session language; Bootstrap 5 RTL build loaded conditionally; **all custom CSS written with logical properties** (`margin-inline-start`, `padding-inline-end`, `inset-inline`) — physical left/right properties are banned in code review. |
| Full mirroring | Sidebar flips to the right; breadcrumbs, steppers, tabs, table column order, kanban column order, pagination, toasts, modals — everything mirrors. The only non-mirrored elements: media controls, clock-based visuals, and logos. |
| Icons | Directional icons (arrows, chevrons, back, next, send) flip in RTL via the icon component (central flip list); non-directional icons never flip. |
| Charts | Category axes mirror (first category starts right); time axes **remain LTR** (time flows left→right universally — with axis labels localized); this is an explicit product decision to avoid misread trends. |

### 1.2 Bidirectional content rules

| Content | Rule |
|---------|------|
| Numbers & money | Always LTR digit runs (`dir="ltr"` inline spans); money right-aligned in tables in both layouts; Arabic-Indic digits (user preference) still keep LTR run order. |
| Codes & official numbers | `STU-26-00042`, phone numbers, emails, URLs: forced LTR spans with direction isolation (`<bdi>`/`unicode-bidi: isolate`) — prevents the classic broken `042-26-STU` rendering. |
| Mixed-language cells | `dir="auto"` + start-alignment per column's dominant language; NameAr columns right-aligned, NameEn left-aligned, in both UI directions. |
| Free text | Editors honor first-strong-character direction with a manual direction toggle for mixed paragraphs (messaging compose). |
| Dates | Rendered per locale (Arabic month names in AR UI; Hijri sub-display per config) as isolated spans. |

### 1.3 RTL acceptance

Every screen ships with **two screenshots in review (AR + EN)**; the QA checklist includes: no clipped text at 120% Arabic line-height (Arabic needs taller lines), no bidi-broken codes, mirrored navigation flows, form label alignment, PDF outputs verified in both languages (Off documents render per-language templates, BR-CRT-002).

## 2. Responsive design

### 2.1 Breakpoints & strategy

Bootstrap defaults (sm 576 / md 768 / lg 992 / xl 1200 / xxl 1400).

| App | Strategy |
|-----|----------|
| Staff app | Desktop-first (lg+ optimal); **fully functional at md** (teacher laptops/tablets); at sm: the mobile-critical subset only — attendance capture, cover console, trip console, POS, approvals inbox, notification center — these six are designed mobile-native (they're used standing up). Dense admin screens (structure builders, matrices) show a "best on larger screen" note below md rather than degrading badly. |
| Portal | Mobile-first; bottom tab bar < md (UI-01 §2); every portal flow completes on a 360px phone; payment/consent flows single-column always. |

### 2.2 Component rules

Tables collapse by priority columns (column chooser defines mobile set; identity column always survives) or switch to card-list at sm (pattern per P-LIST config); touch targets ≥ 44px on capture/POS/portal; hover-only affordances banned (every hover action has a tap path); modals become full-screen sheets at sm; print layouts unaffected by responsive state.

## 3. Accessibility (WCAG 2.1 AA target — NF-L7)

| Area | Standard |
|------|----------|
| Contrast | AA ratios (4.5:1 text, 3:1 large/UI) enforced in the token palette (UI-01 §6); status chips carry text labels — **color never the sole signal** (attendance statuses letter+color, chart series pattern+color). |
| Keyboard | Everything operable by keyboard (doc 02 §3 is also the a11y path); visible focus rings (never suppressed); focus order follows visual order in both directions; skip-to-content link; focus-trap in modals. |
| Screen readers | Semantic landmarks (nav/main/aside); ARIA on composite widgets (tabs, boards, grids per WAI-ARIA patterns); `lang` attributes switch per bilingual field (Ar field `lang="ar"`) so screen readers switch voices; live regions for toasts and live consoles (polite) and safety alerts (assertive). |
| Forms | Programmatic label association; error summary linked (doc 02 §2); required state in ARIA not just asterisk. |
| Media & docs | Icons aria-hidden with text alternatives; official PDFs tagged (heading structure) where the T-5 engine supports — engine selection criterion added. |
| Motion & time | No content behind animation; session-timeout warning with extend action (BR-SEC-004 UX); auto-refresh consoles pause on focus-within (no data yanked mid-action). |
| Testing | Automated axe scan in CI per screen + manual audit per release for the top-20 screens (both languages — Arabic screen-reader testing explicitly in scope); a11y issues classified as defects, not enhancements. |

Scope note: AA is contractual-target for **portal** and the six mobile-critical staff screens; remaining staff screens meet AA with documented exceptions reviewed per release (pragmatic enterprise posture — exceptions list is public in release notes).

## 4. Performance UX budgets

Aligned to NF-P3/P4: first contentful paint ≤ 1.5 s on school broadband; capture-sheet interaction ready ≤ 2 s for 40 students; bundle discipline (no per-module JS frameworks — jQuery-thin per T-4); images lazy-loaded (student photos in rosters paged); offline-tolerant screens (POS, trip, capture) use background sync queues with visible state (doc 02 §6).

## 5. Open questions

1. Arabic screen-reader test matrix (NVDA + Windows voices vs VoiceOver Arabic) — fix the supported pair at implementation start. |
2. Formal a11y conformance statement (public VPAT-style) — produce for tenders? Recommend yes at GA. |
3. Tablet-specific optimizations for teachers (md portrait) beyond responsive defaults — gather pilot telemetry before investing. |
