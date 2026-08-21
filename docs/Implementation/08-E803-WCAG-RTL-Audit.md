# E-803 — WCAG 2.1 AA + RTL Audit (S8 Hardening)

**Date:** 2026-08-20 · **Scope:** every screen in `sms/src/Sms.Web` as of commit `a8163a1` (staff shell + login, S1 setup/academic screens, people & operations screens incl. finance/grading/timetable, parent portal — ~100 views, `site.css`, `site.js`, `quickadd.js`, `translit.js`).
**Method:** four parallel code-audit sweeps (shared shell, setup/academic, people/ops, portal); every finding verified against the actual markup/CSS, not assumed. Repeating patterns are reported once with counts.
**Out of scope:** the PDF-acceptance half of E-803 — still blocked on the O6 PDF-engine decision (open item, docs/Implementation/01-Entry-Decisions).

---

## 0. Verdict

**The RTL foundation is sound; the accessibility layer is systematically thin.** Direction handling (culture cookie → per-layout `dir`/`lang` → `bootstrap.rtl.min.css` swap, logical CSS properties throughout) was built correctly from the start — zero legacy `ml-*/mr-*/text-left/float-*` utilities exist anywhere in the Views tree. What is missing is almost entirely *programmatic semantics*: labels not tied to inputs, tables without `scope`, icon buttons without names, bidi isolation on Latin tokens, and a contrast token that fails off-white surfaces. Most findings are mechanical, pattern-shaped, and fixable in sweeps.

| Severity | Systemic patterns | Individual findings (approx. instances) |
|---|---|---|
| High | 6 | ~20 items + ~460 instances via patterns |
| Medium | 9 | ~45 items |
| Low | — | ~30 items |

---

## 1. Foundation verified correct — do not "fix"

- **RTL mechanism**: `Startup.cs:626` request localization (`en-US`/`ar-SA`), culture cookie via POST `Home/SetLanguage`; each of the three layouts (`_Layout`, `_LoginLayout`, `_PortalLayout`) computes `isRtl` and emits `<html lang dir>` + swaps the Bootstrap 5.3.3 RTL bundle. Translation is inline `T(en, ar)` ternaries (no .resx).
- **Zero** legacy directional utilities and **zero** inline `left:/right:` styles in any view; `site.css` is logical-property-first (`inset-inline-start`, `border-inline-start`, `padding-inline`); `.sms-flip` mirrors directional icons where used.
- Staff layout has a working skip link (`_Layout.cshtml:29` → `#sms-main`); flash/error banners carry `role="alert"` in both shells; auth forms are fully labeled with `dir="ltr"` correctly forced on credentials/OTP; `_QuickAddLookup` isolates AR/EN name fields correctly.
- Bootstrap modals (`_HelpModal`, `_QuickAddLookup`) have correct dialog semantics and focus handling; the admissions kanban moves cards via forms (no drag-only interaction); phone/email fields are `dir="ltr"`-isolated in ~10 places.
- Portal viewport allows zoom (no `user-scalable=no` anywhere); portal timetable grid reads correctly in RTL via logical day ordering + the RTL bundle.

## 2. Systemic findings — fix once, sweep everywhere

**S1 · HIGH — Labels not programmatically tied to inputs.** ~240 orphan `<label>` elements (132 in setup/academic, 108 in people/ops), ~35 placeholder-only controls, and **zero `aria-label` on any input in either area**. Worst: `Grading/Marksheet.cshtml:55` (score grid — one unnamed number input per student × component), `Fees/Structure.cshtml:72,83` (money grid), `Grades/Index.cshtml:92-104`, `Rooms/Index.cshtml:58-67`. The `asp-for` forms already emit input `id`s — the fix is `asp-for`/`for` on the label; grid cells need per-cell `aria-label`. (WCAG 1.3.1/3.3.2/4.1.2)

**S2 · HIGH — Icon-only controls without accessible names.** 24 buttons with *no* name at all (list in §4.2 of the people/ops sweep; incl. terminate-contract, cancel-session, the cover console's only date stepper, and two with no `title` either: `Sections/Details.cshtml:39`, `Rooms/Details.cshtml:38`), ~40 more named only via `title` (fragile), plus the shell's user-menu button whose visible name is `d-none` below the `md` breakpoint in **both** layouts (`_Layout.cshtml:81`, `_PortalLayout.cshtml:43`) and the untranslated `title="Jobs"` Hangfire link (`_Layout.cshtml:79`). Fix: `aria-label="@T(...)"` per control. (4.1.2/2.4.4)

**S3 · HIGH — Zero `scope`, zero `<caption>` in the entire Views tree** (grep-verified). Breaks the true 2-D grids: `_TimetableWeekGrid.cshtml:15,19`, `Timetable/Builder.cshtml:84,88`, `Teachers/Matrix.cshtml:43,47` (row headers are `<td>`, not `<th>`), `Subjects/Index.cshtml:102` (qualification matrix, empty cell = "not qualified"), plus headless tables `Payments/Allocations.cshtml:44` and `Sections/Details.cshtml:49` (no `<thead>` at all). (1.3.1)

**S4 · HIGH — No bidi isolation on Latin tokens in Arabic context.** Document numbers reorder visually in RTL: `INV-2026-000123` renders as `000123-2026-INV`, `RCP/SCHOOL/2026/000123` scrambles (30 `<code>`-wrapped occurrences in people/ops alone: `Fees/Index.cshtml:61`, `Payments/Receipt.cshtml:33`, `Students/Index.cshtml:25`…). Signed amounts detach from their sign (`Fees/Position.cshtml:55`, `Payments/Till.cshtml:56` — the till variance sign can migrate, `Fees/Charge.cshtml:71`). Dates/ranges glued to Arabic (`منذ 45`, `1.2–3.4`, ISO dates after `·`). The codebase already isolates mobiles correctly — the convention exists, these were missed. Fix: one CSS rule `.sms-table code, code.sms-no { unicode-bidi: isolate; direction: ltr; }` + `<bdi dir="ltr">` for signed amounts and inline tokens. (RTL, 1.3.2)

**S5 · HIGH/MED — Contrast.** `text-warning` used as foreground text = **1.63:1** (`Portal/Student.cshtml:43` attendance headline, `_TimetableWeekGrid.cshtml:40` substitute name). `--sms-muted #6b7280` passes on white but fails on `--sms-body-bg #f4f6fb` (~4.47:1) in 7 verified rules: `.sms-subnav .nav-link` (nearly every module page), `.sms-section-title`, `.sms-stepper__num`, kanban off-column heads, calendar weekend cells, `.sms-cal-day__hijri` (also ~8.7px), `.sms-auth__foot`. Fix: darken the token to `#5b6270` and replace foreground `text-warning` with a dark amber (`#8a6100`) or `badge text-bg-warning`. (1.4.3)

**S6 · MED — Colour-only signalling.** Fee due/settled red-green (~10 sites incl. portal ×4), attendance banding thresholds (portal), calendar day types + `has-event` dot + colour-swatch legend, teacher load meters (amber "under half" only in colour; no `role="progressbar"` on any `.sms-progress`, 5+ sites), kanban `is-late` red border, stepper states, band-preview chart (`Grading/Scales.cshtml:76` — `title`-only, keyboard-unreachable). Fix: text token / visually-hidden word / `role="progressbar"` + value per site. (1.4.1)

**S7 · MED — Navigation semantics.** `aria-current` appears **0 times** in the tree; all 8+ sub-nav partials and the portal tab bar are bare `<ul>`s outside `<nav>` landmarks; active state is colour-only. Sidebar is correct on collapse groups but its active item also lacks `aria-current`. Fix: wrap in `<nav aria-label>`, add `aria-current="page"`. (1.3.1/4.1.2)

**S8 · MED — Forms: required + errors.** 0 `aria-required`/asterisks in both screen areas; the four registration forms are `novalidate` with mandatory fields unmarked; `asp-validation-for` unused outside Account; most POSTs report via `TempData["Error"]` with the offending field neither identified nor focused; `School/Profile.cshtml` can fail validation into a hidden tab. (3.3.1/3.3.2/3.3.3)

**S9 · MED — Language of parts.** 87 `dir=` attributes vs **0 `lang=`** on bilingual fields/cells; deliberate opposite-language strings (timetable day/subject names, the language-toggle label ×3) carry no `lang`; raw English enums/codes leak into the Arabic UI (~20 sites: `StudentStatus`, `GenderPolicy`, `WingTag`, `AudienceScope`, announcement scopes, parent dedup flags, checklist codes, `carry-forward`/`void` badges, `Home/Index` school/year statuses) — label helpers exist (`StaffLabels`, `FinanceLabels`…) but Students/Parents and these spots bypass them. `Error.cshtml` and `Privacy.cshtml` are English-only under `lang="ar"`. (3.1.2)

**S10 · MED — Directional glyphs in copy.** `→` inside 12 Arabic literals points backwards (U+2192 is not bidi-mirrored): status-flow prose in `School/Status`, `AcademicYears`, `Rooms`, `Subjects/Plan`… Conversely the portal pre-flips `←` which the bidi algorithm then mirrors **back** (`Portal/Index.cshtml:42`, `Portal/Student.cshtml:75`). Glyph-only signals `↔`/`⚠`/`🔒`/`✓` lack text equivalents (timetable substitution/room-change, locked slots, guardian flags, staff-nav lock emoji). Directional copy that lies in RTL: "add the first unit **on the right**" while the RTL panel renders left (`Employees/Org.cshtml:41`, `Timetable/Shape.cshtml:78`). (RTL, 1.1.1)

**S11 · MED — Decorative icons.** 359 `<i class="bi …">` in the tree, **0** with `aria-hidden="true"` — every icon is announced as garbage glyphs alongside its label. Ten-line fix in the shared partials covers most. (1.1.1)

**S12 · HIGH — Keyboard/JS interaction gaps.**
- Mobile drawer (`site.css:164`, `site.js:15`): hidden only by `transform`, so off-screen links stay tabbable; no focus management; Escape handler steals from modals.
- Calendar day grid (`Calendar/Index.cshtml:51,116`): clickable `<td>`s — no `tabindex`, no key handler; the paint-days workflow is **mouse-only**.
- `onchange="this.form.submit()"` on 7 filter selects (3.2.2).
- The charge-void reason on `/fees` (`Fees/Index.cshtml:77`) uses `prompt()` — works with a keyboard but loses input on failure and is suppressible; replace with a labelled modal. *(Introduced 2026-08-20 by the void feature; flagged same day by this audit.)*
- `Setup/Step.cshtml:67`: all country-pack radios share `id="PackCode"` (duplicate IDs, ambiguous labels).
- Bootstrap tab widgets missing tab ARIA (`School/Profile.cshtml:22`, `Portal/Student.cshtml:32` — no `role="tab"`/`aria-selected`/`tabpanel`, so arrow-key nav is dead and pane changes silent).
- `Marksheet` abs/exempt toggles silently disable+clear the score input (no live region; `disabled` drops out of tab order under the user).
- Portal 15-min idle sign-out (`PortalReauthAttribute.cs:23`): no warning, no extend affordance; `_PortalLayout` has no `Scripts` section to even add one; its bilingual-concatenated error string bypasses `T()`. (2.2.1)

## 3. High-severity individual items (not covered above)

| # | Location | Issue |
|---|---|---|
| 1 | `_PortalLayout.cshtml:35,48` | No skip link + unlabelled `<main>` in the portal (CSS for it already exists) |
| 2 | `Teachers/Matrix.cshtml:64` | BR-TCH-004 load-override checkbox has an **empty accessible name** (label wraps it with no text; `title` on the label names nothing) |
| 3 | `Timetable/Cover.cshtml:88-90` | Destructive cancel-session / change-room path entirely unnamed inside an unlabelled `<details>` |
| 4 | `Payments/Allocations.cshtml:44` | 5-column allocation table with no header row at all |
| 5 | `Grading/Marksheet.cshtml:55-56` | Primary teacher workflow: unnamed score grid; «غ» as the absent checkbox's whole name |

## 4. Remediation plan (proposed phases)

| Phase | Work | Shape | Est. |
|---|---|---|---|
| **P1 — mechanical sweeps** | `scope="col/row"` + `<caption>`; `aria-hidden` on icons; `aria-label` on the 24+40 icon buttons; `code` bidi-isolation CSS rule + `<bdi>` on signed amounts; darken `--sms-muted`; replace foreground `text-warning`; `aria-current` + `<nav>` wraps; `:focus-visible` block | Pattern edits across ~75 views + one CSS pass; no behaviour change | 1–2 sessions |
| **P2 — forms** | Tie ~275 labels (`asp-for`/`for`/`aria-label`); `required aria-required` + asterisk legend; `asp-validation-for` spans on the registration forms; fix duplicate `PackCode` ids | Mechanical but wide | 1–2 sessions |
| **P3 — interaction rework** | Calendar day buttons + key support; drawer focus management (`visibility`, `inert`, focus restore); tab ARIA (Profile + portal Student); void-reason modal (replaces `prompt()`); marksheet live region + `readonly`; portal idle-timeout warning hook | Real JS/markup work, needs manual testing | 1 session |
| **P4 — language & copy** | `lang` on bilingual runs; enum label helpers (Students/Parents + stragglers); localize `Error`/`Privacy`/re-auth message; fix `→`/`←` arrows; de-directionalize copy ("on the right"); glyph text equivalents | Mechanical | 1 session |
| **P5 — PDF acceptance** | Blocked on **O6** (QuestPDF vs Syncfusion vs DevExpress spike) | — | — |

Colour-only signalling (S6) items are folded into P1 (text tokens) and P3 (progressbar roles) as they're touched.

## 5. Sign-off

E-803 **audit half: complete** (this document). **Fix pass: pending owner go-ahead** on phases P1–P4. **PDF half: blocked on O6.** Re-audit recommendation: after P1–P4, spot-check with NVDA + Chrome (AR + EN) on the four highest-traffic screens (marksheet, cashier, charge explorer, portal student file) before calling BR-level closure.
