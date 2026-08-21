# O6 spike — QuestPDF on .NET 5 (E-803, 2026-08-18)

**Status:** spike executed for the QuestPDF candidate only; **result recorded as the IP-02 §4 addendum below**. Syncfusion / DevExpress not exercised (commercial packages, no licence available in this environment). Spike code is throwaway per ground rule 2 — kept here as evidence, never merged into `sms/`.

## What was rendered

Both IP-02 §4 acceptance documents from fixed fixtures (`Program.cs`):

- (a) Arabic report card — RTL table, mixed AR/EN subject names, Hijri + Gregorian dates, Arabic-Indic digits → `report-card-ar.pdf` / `.png`
- (b) ZATCA simplified tax invoice — real Phase-1 TLV QR (tags 1–5, Base64), 15 % VAT lines → `tax-invoice-zatca.pdf` / `.png`

Versions tried: QuestPDF **2022.12.15** and **2023.12.6** — the last releases that restore on `net5.0` (2024.x+ targets net6+ only). Runtime note: this machine has no net5.0 runtime, only 6.0.36; the spike ran with `<RollForward>Major</RollForward>` (same as the product test suite must be doing).

## Findings against the fixed criteria

| Criterion | Result | Evidence |
|---|---|---|
| Arabic glyph shaping / joining | ✅ Correct in both versions | all Arabic words render joined and legible |
| Page-level RTL layout (columns, tables, header/footer mirroring) | ✅ `ContentFromRightToLeft()` mirrors correctly | table columns right-to-left, signature blocks mirrored |
| **Bidirectional runs inside a paragraph** | ❌ **FAIL** — no Unicode bidi reordering. Latin runs embedded in RTL text come out mirrored (`Mathematics` → `scitamehtaM`, `English Language` → `egaugnaL hsilgnE`); every digit sequence — ASCII *and* Arabic-Indic — is reversed (`100` → `٠٠١`, `2026-2027` → `7202-6202`, `13,800.00` → `٠٠.٠٠٨,٣١`). Same in 2022.12 and 2023.12; `DirectionAuto()` (2023.12) does not fix it — it only sets paragraph direction, not run reordering. | `report-card-ar.png` rows 3–5, totals line; `tax-invoice-zatca.png` all amounts |
| Embedded Arabic fonts | ⚠️ System font (Tahoma) used; embedding a bundled font (e.g. Noto Naskh Arabic) is supported via `FontManager.RegisterFont` — not exercised | — |
| Tagged (accessible) PDF | ❌ Not supported in any QuestPDF version restorable on net5.0 | QuestPDF docs; no API surface for it |
| Code ergonomics | ✅ Fluent C#, ~200 lines for both documents | `Program.cs` |
| Licence | Community licence free below the revenue threshold; **Professional licence required** once the product earns revenue (2023.x+ enforces `Settings.License`) | 2023.12.6 requires the setting |

**Verdict: QuestPDF is not acceptable for KSA-01 documents on the CR-2 (.NET 5) runtime.** A report card that prints `٠٠١` for 100 or an invoice printing reversed amounts fails RTL fidelity outright — a numeric-inversion defect on a tax invoice is a legal problem, not a cosmetic one. Correct bidi (and tagged PDF) arrives in QuestPDF only with the 2024.3+ text engine, which needs net6.0+; the same is true of the modern Syncfusion/DevExpress lines (net6+/netstandard2.0 builds vary — to be verified if either is evaluated).

## Consequences / options for the tech lead

1. **The O6 decision is now coupled to CR-2.** Every shortlisted engine's *current* release with proper bidi is net6+/net8; the net5-compatible releases are 2022-era. This is the first place the CR-2 (.NET 5) override produces a concrete functional blocker rather than a support-window risk — it should be re-surfaced to the owner (the memory note already flagged "re-surface once before portal go-live"; this is earlier and harder evidence).
2. **Workaround inside .NET 5** (if CR-2 stands): pre-apply the Unicode Bidi Algorithm app-side (run reordering + digit runs kept LTR) before handing strings to QuestPDF — i.e. produce *visual-order* strings. Feasible (a UAX #9 implementation is ~1–2 dev-days, or a small library), but fragile: it must be applied to every string on every template, wrapping/line-breaking of pre-reordered text is wrong for multi-line cells, and tagged PDF is still absent. Not recommended as the product answer; acceptable only as a pilot stop-gap for single-line fields.
3. **Evaluate Syncfusion PDF (netstandard2.0 build)** — its `PdfTextElement`/RTL support historically handles bidi and digits on older frameworks; commercial licence needed. Cannot be exercised here without a licence.

## Impact on E-803's other half

- **PDF acceptance per language** (golden AR/EN files, tolerance diff): cannot start until an engine passes this spike; the fixtures in `Program.cs` are reusable as the golden-file inputs.
- **WCAG 2.1 AA / RTL screen audit**: still blocked — no Razor screens exist beyond Home/Privacy (see build log). axe-core E2E scan has no surface to run against.

E-803 therefore stays **open**, but its blocker is now a *decision* (O6 engine + CR-2 runtime), not an unknown.
