# Module 01 — System Setup

**Phase:** 3 — Academic structure | **Status:** Draft for review | **Rule prefix:** `BR-SET`

---

## 1. Purpose

The configuration foundation of the product: bilingual reference data (lookups), school-level settings, country packs, and feature toggles — so that onboarding a new school is configuration only (BO-06) and no school-specific behavior ever requires code (NF-M2).

## 2. Scope

**In:** product-seeded and school-managed lookup lists; school settings hub (currency, time zone, calendars, working week, languages); country pack selection; feature toggles; numbering configuration UI (engine per doc 08); notification/attachment configuration entry points (docs 09/10); setup wizard for new schools.
**Out:** user/role management (Module 36), school profile itself (Module 02), academic year creation (Module 03), grading scales (Module 17), fee structures (Module 19).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-SET-001 | Lookups are two-tier: **product-seeded** (nationalities, ISO currencies, blood types, ID types, relationship types — updatable by product releases, not editable by schools) and **school-managed** (housing types, referral sources, custom tags…). Both bilingual per BR-GLB-001. |
| BR-SET-002 | A lookup value referenced anywhere is deactivatable, never deletable (BR-GLB-005/006). |
| BR-SET-003 | Every school completes the **Setup Wizard** before its first academic year can be activated: profile, country pack, currency, time zone, working week, languages, calendar type, numbering series, stage structure. The wizard tracks completion per step. |
| BR-SET-004 | Country pack selection binds: VAT defaults, ID-type requirements (BR-GLB-003), Hijri display default, retention defaults (BR-AUD-006), statutory report set. Changing country pack after go-live requires product-support permission (T1-audited). |
| BR-SET-005 | Year-versionable settings (working week, VAT rate, thresholds) are effective-dated per academic year (BR-GLB-011); historical transactions always display the setting in force at their date. |
| BR-SET-006 | Feature toggles (portal, student accounts, cafeteria, store, transport…) hide entire modules from menus and permissions when off (deny-by-default composition with doc 06); toggling off never deletes data. |
| BR-SET-007 | All configuration changes are audited: settings T1, lookups T3 (doc 07). |
| BR-SET-008 | Default notification subscriptions, document-type catalogs, and workflow definitions ship enabled per docs 09/10/05; the wizard offers per-school adjustment, not per-school invention. |

## 4. Workflow

Setup Wizard is a guided checklist (not an approval workflow): steps can be revisited until "Setup Complete" is declared, which requires all mandatory steps green. Post-go-live settings changes use direct edit (audited) except country pack (support-gated) and financial-series changes (P2 per BR-NUM-005).

## 5. User roles

System Administrator (full), Principal (view + selected settings), Product Support (country pack, license), Finance Manager (financial settings sections only).

## 6. Permissions

| Screen | View | Edit | Configure |
|--------|------|------|-----------|
| Lookup lists | Sys Admin, Principal | Sys Admin (school tier) | — |
| School settings hub | Sys Admin, Principal | Sys Admin | Sys Admin |
| Financial settings (currency, VAT) | + Finance Manager | Finance Manager + Sys Admin | P2 approval |
| Feature toggles | Sys Admin | Sys Admin | — |
| Setup wizard | Sys Admin | Sys Admin | — |

## 7. Database concept

Entities: `LookupCategory` (seeded/school tier flag) → `LookupValue` (bilingual, active flag, sort); `SchoolSetting` (key, value, value type, effective academic year, school); `CountryPack` (product-defined bundle); `FeatureToggle` (per school); `SetupChecklist` (per school, step status). All school-scoped rows carry SchoolId (ADR-2). Settings history preserved via effective-dating rather than overwrite.

## 8. Required screens

1. **Setup Wizard** — stepper with completion tracking, per-step validation, bilingual entry side-by-side (Ar/En fields adjacent).
2. **Lookup management** — category tree, value grid (Ar/En/sort/active), usage counter before deactivate.
3. **School settings hub** — grouped tabs: Regional (TZ, calendars, week), Financial (currency, VAT), Languages, Portal, Numbering (embeds doc 08 registry), Notifications defaults, Document types.
4. **Feature toggles** — module list with on/off + dependency warnings (e.g., Transport fees require Transport).
5. **Country pack viewer** — read-only contents of the active pack.

## 9. Validation rules

Wizard steps validate on save (server-side, BR-GLB-110); currency/TZ from ISO lists only (BR-GLB-112); both language names mandatory on every lookup; working week must contain ≥ 4 working days; deactivation of a lookup shows usage count and requires confirmation; VAT rate change requires effective date ≥ today.

## 10. Reports

Configuration snapshot report (full settings dump for support/audit, bilingual) · Lookup usage report · Settings change history (from audit, filtered view) · Setup completeness report (product/sales onboarding tracker).

## 11. Dashboard widgets

Sys Admin dashboard: setup completeness %, recent configuration changes, disabled features list.

## 12. Notifications

`SetupStepCompleted` → Sys Admin (in-app); `SettingChanged` (financial keys) → Principal + Finance Manager; `CountryPackChanged` → Principal (always).

## 13. Future enhancements

Multi-school template inheritance (group defines defaults, schools override); configuration export/import between environments; guided "new year settings review" checklist each rollover.

## 14. Open questions

1. Which lookups must remain product-tier vs school-tier? Starter split proposed in BR-SET-001 — confirm with pilot school.
2. Should VAT config support multiple rates simultaneously (education exempt, transport standard-rated) in v1? Recommendation: **yes** — per fee category rate mapping (coordinates with Module 19).
3. License/subscription enforcement (student-count tiers) — product decision needed before Module 36.
