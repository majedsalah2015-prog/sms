# Module 17 — Grading

**Phase:** 5 — Academic operations | **Status:** Draft for review | **Rule prefix:** `BR-GRA`

---

## 1. Purpose

Own the calculation truth: configurable grading scales per curriculum, assessment blueprints' weights, term/year aggregation, pass/fail and promotion criteria, GPA, ranking, and the result documents pipeline (report cards, transcripts) — turning raw marks (Module 16 + continuous assessment) into published, approved, immutable results.

## 2. Scope

**In:** grading scale designer (numeric→band mappings, descriptors, GPA points), mark types (numeric, rubric/descriptor for KG, pass/fail), blueprint weights & calculation rules (with BR-EXM-002), continuous-assessment marksheets, WF-07 approval chain, result computation & locking, pass/promotion criteria (feeding BR-AYR rollover step 3), GPA & ranking policies, report card generation & publication, transcript compilation (multi-year), re-mark/appeal flow (WF-08 extension), makeup application policy (BR-EXM-008 execution).
**Out:** exam logistics (Module 16), certificate documents (Module 18 — consumes final results), scale-independent achievements (Module 10 tab).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-GRA-001 | **Grading scales** are configurable per curriculum designation (BR-GRD-003) and stage: band table (min–max % → band code, bilingual label, descriptor, GPA points, pass flag); scales are year-versioned and lock once results reference them. Multiple scale types: percentage bands (A–F / ممتاز–ضعيف), 4.0 GPA, IGCSE letter grades, KG descriptor rubrics (no numerics on report card). |
| BR-GRA-002 | **Mark types** per blueprint component: numeric (max-bound), rubric level, or pass/fail; KG/lower-primary can run fully rubric-based report cards (per-skill descriptors per subject — skill lists configurable per offering). |
| BR-GRA-003 | **Calculation** per offering per term: weighted components (BR-EXM-002 blueprint) → term score; year aggregation: configurable term weights → final score → scale band. Rounding policy explicit (config: 2dp, round-half-up at final band only — no cascading rounds); calculation is centrally versioned — every published result stores its calculation snapshot (inputs, weights, scale version) for permanent reproducibility. |
| BR-GRA-004 | **Exclusions:** exempted offerings (BR-STU-005) excluded from the student's aggregate per policy (redistribute weights vs reduce denominator — config); non-assessable offerings never enter (BR-SUB-003); attendance % appears on report cards from the single BR-ATD-009 computation. |
| BR-GRA-005 | **WF-07 chain:** marksheet `Draft (teacher entry) → Submitted → HoD-Reviewed → Registrar/Exams-Approved → Published`; each step scope-checked; publication is per grade/section batch (a section's report cards release together); post-publication changes only via **WF-08** (P4, Principal always, reason mandatory, parent re-notified, recalculation cascades to aggregates/ranks with full audit T1). |
| BR-GRA-006 | **Pass/promotion criteria** per grade-year (feeding BR-AYR-008 step 3): overall pass mark, per-subject minimums, max failed-subjects for promotion, makeup-exam gates (failed subject → makeup → capped mark per BR-EXM-008 policy), conditional promotion definition; criteria produce the promote/retain/conditional proposals the rollover consumes. |
| BR-GRA-007 | **GPA & ranking:** GPA per configured formula (weights from BR-SUB-002 offering weights × scale points); ranking per section and per grade with explicit tie policy (config: shared rank standard-competition style default); rank visibility is school-configurable (on report card / internal only / top-N honor list only) — privacy-aware default: internal + honor list. |
| BR-GRA-008 | **Report cards:** template per stage/curriculum (bilingual, marks + bands + descriptors + attendance + homeroom comment + behavior summary per policy); generated as immutable PDFs at publication (doc 10 stored, numbered per Module 18 pattern for official copies); portal publication per BR-NOT catalog; reprints watermark "Copy". |
| BR-GRA-009 | **Transcripts:** multi-year compilation from published results only (incl. external records BR-ADM-009, source-labeled); issued via Module 18 (numbered, verified); transcript layouts per curriculum (GPA-based vs percentage-based). |
| BR-GRA-010 | Homeroom/teacher comments on report cards come from configurable comment banks (bilingual) + free text with moderation flag (Registrar review queue if enabled). |
| BR-GRA-011 | All marks T1-audited from first entry; scales/blueprints/criteria T1 on change after first use; publication events T1. |

## 4. Workflow

WF-07 as BR-GRA-005 (the module's spine). WF-08 post-publication corrections (P4). Appeal flow: parent requests re-mark (portal, window N days post-publication) → HoD review → outcome (uphold/adjust via WF-08) → parent notified; appeal register kept. Criteria/scale changes mid-year: P2 Principal + impact display (recalculation preview).

## 5. User roles

Registrar/Exams Officer (publication owner), HoD (review step), Teachers (entry + comments), Principal (WF-08, criteria approvals), Homeroom (comments, section overview), Parents/Students (portal results, appeals), Auditor (calculation snapshots).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Configure scales/criteria | Registrar + Principal approval |
| Enter marks/comments | Assigned teacher (marksheet scope) |
| Review/approve sheets | HoD (department), Registrar (publish) |
| Publish results | Registrar (batch, P2 final step) |
| WF-08 corrections | Principal chain only |
| View results pre-publication | Chain roles only (teachers: own sheets) |
| Portal results | Parent/Student (published only, BR-SEC-012) |
| Appeals | Parent (window-bound) |

## 7. Database concept

Entities: `GradingScale` + `ScaleBand` (versioned); `Blueprint` + `BlueprintComponent` (with Module 16, weights here); `Marksheet` + `MarkEntry` (single marks store per BR-EXM-007 note); `SkillRubric` structures (KG); `TermResult` / `YearResult` (computed-persisted with calculation snapshot); `PromotionProposal` (criteria output → BR-AYR); `Rank` (per scope, tie-policy applied); `ReportCardIssue` (immutable PDF ref, number); `Appeal` (workflow-managed). Calculation engine is a domain service — one implementation, snapshot-versioned (BR-GRA-003).

## 8. Required screens

1. Scale designer — band table editor with visual preview, GPA points, bilingual descriptors.
2. Blueprint & weights editor (with Module 16) — per offering-term, weight sum = 100 validation, lock indicators.
3. Criteria designer — pass/promotion rules per grade-year with plain-language preview ("Student passes if…").
4. **Marksheet workspace** — teacher grid (numeric/rubric modes), progress save, validation, submit; HoD review queue with distribution charts (outlier detection aid); Registrar publication console (batch by grade/section, completeness check, publish button behind confirmation).
5. Results explorer — internal: student/section/grade result views, rank tables, failure lists, makeup candidates.
6. Report card center — template preview per stage, batch generate, publication + portal release, reprint (watermarked).
7. Transcript builder — multi-year view per student, external-record inclusion, issue via Module 18.
8. Appeal console — requests queue, window enforcement, outcome recording.
9. Portal: results per child (published), report card download, appeal request.

## 9. Validation rules

Weight sums = 100 per blueprint; marks within component max/mark-type; sheet submission requires all students resolved (mark, absent-classified, or exempt); publication requires all sheets of the batch approved + attendance % available; scale band ranges contiguous/non-overlapping; criteria referential integrity (subjects exist in plan); WF-08 requires reason + recalculation acknowledgment; appeal only within window; comment length/moderation rules. |

## 10. Reports

Result sheets per section (broadsheet: students × subjects) · Pass/fail statistics per subject/section/grade (with year-over-year comparison) · Subject difficulty analysis (average, distribution, outlier sheets) · Honor lists (top-N per grade) · Failure & makeup candidates list · Promotion proposals register (rollover feed) · GPA distribution · WF-08 corrections register (T1 view) · Appeals register & outcomes · Ministry result formats per pack. |

## 11. Dashboard widgets

Registrar: sheets pipeline (entered/reviewed/approved/published %), publication readiness per grade. HoD: my department sheet status, subject averages vs school average. Principal: pass-rate summary, pending WF-08/appeals. Teacher: my sheets due/returned. Portal: latest published results card.

## 12. Notifications

`MarksheetReturned` (HoD → teacher) → teacher with notes; `ResultsPublished` → parents/students (portal link, no marks in SMS per BR-NOT-010); `MarkChangedAfterPublication` → parents (mandatory, WF-08); `AppealWindow` (opens/closes) → parents; `AppealDecided` → parent; `SheetsOverdue` → teacher + HoD; `PromotionProposalsReady` → Registrar (rollover trigger). |

## 13. Future enhancements

Standards-based grading (learning-outcome mastery tracking); predictive grade alerts (mid-term trajectory); external exam results import (IGCSE board results reconciliation); analytics: value-added measures per teacher/subject (sensitive — governance needed); parent-teacher conference scheduling tied to results release.

## 14. Open questions

1. Exclusion policy default for exemptions (BR-GRA-004): redistribute vs reduce denominator — confirm per curriculum norms (proposed default: reduce denominator). |
2. Rank visibility default (proposed internal + honor list) — confirm market expectation (some schools print rank on report cards). |
3. Appeal window default (proposed 5 working days) and whether appeals require a fee in any target school? |
4. KG rubric skill lists: product starter sets per curriculum or school-defined only? Proposed: starter sets in country packs. |
