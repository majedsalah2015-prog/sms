# Module 05 — Grades (Stages & Grade Levels)

**Phase:** 3 — Academic structure | **Status:** Draft for review | **Rule prefix:** `BR-GRD`

> "Grade" here = grade level / صف (glossary §8), never a score.

---

## 1. Purpose

Model the school's educational ladder — stages (KG, Elementary, Intermediate, Secondary) and ordered grade levels within them — as the structural backbone that sections, curriculum, fees, promotion paths, and permission scopes all hang on.

## 2. Scope

**In:** stage catalog per school, grade levels (ordered, bilingual, coded), promotion path (next-grade mapping incl. graduation terminus), curriculum designation per grade, gender policy per stage/grade, capacity planning per grade per year, age-eligibility rules per grade.
**Out:** sections (Module 06), subject-to-grade curriculum mapping (Module 07), promotion *criteria* (Module 17), fee amounts per grade (Module 19).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-GRD-001 | Stages come from the school's offered set (BR-SCH-003); each stage holds ordered grade levels with unique codes and bilingual names (e.g., G5 / الصف الخامس). Product ships country-pack stage/grade templates (KG1..G12 variants); schools adjust in setup. |
| BR-GRD-002 | Every grade (except terminal ones) declares exactly one **promotion target** (next grade); terminal grades are flagged Graduating (drives rollover step 3 exit-to-Graduate, BR-AYR §4). Cross-stage promotion follows the ladder (G6 → G7 across stages). |
| BR-GRD-003 | Each grade carries a **curriculum designation** (national / American / IGCSE / IB / custom — lookup, BR-SET-001) per academic year; grading scales and subject plans bind to it (Modules 17/07). Dual-curriculum schools model parallel grade tracks (e.g., G10-National, G10-IGCSE) as distinct grades sharing a stage. |
| BR-GRD-004 | **Gender policy** per stage or grade: Mixed / Boys / Girls (README Q3 market reality). Sections inherit and may narrow (mixed grade with gendered sections); enrollment validates student gender against the effective policy (BR-GLB-024 context). |
| BR-GRD-005 | **Age eligibility** per grade: min/max age at a configurable cutoff date (e.g., age 6 by Sep 1 for G1) per country pack; Admissions validates against it with permission-gated override (logged with reason). |
| BR-GRD-006 | **Capacity plan** per grade per academic year: target sections × section size = planned seats; Admissions consumes remaining-seat counts (waiting list trigger); plan changes after enrollment begins are T2-audited. |
| BR-GRD-007 | A grade with any historical enrollment is deactivatable only (BR-GLB-005); reordering the ladder never rewrites history (order stored per year — historical years keep their structure). |
| BR-GRD-008 | Grade structure is year-versioned: rollover copies the active structure into the Preparation year where it can be adjusted (new track added, section targets changed) without touching the running year. |

## 4. Workflow

Structure editing is direct entry (audited) during Setup/Preparation; changes to the **active year's** structure (adding a grade mid-year, changing gender policy) require P2 approval (Principal) with impact display (existing enrollments, sections). No approval chain for Preparation-year edits.

## 5. User roles

Sys Admin / Registrar (author), Principal (active-year change approver), Stage Supervisors (view own stages — scope source, doc 06 §4.2), Admissions Officer (consumes capacity).

## 6. Permissions

| Action | Roles |
|--------|-------|
| View structure | All staff (their scopes) |
| Edit Preparation-year structure | Sys Admin, Registrar |
| Edit Active-year structure | Registrar + Principal approval (P2) |
| Edit capacity plan | Registrar; Admissions Officer (view) |
| Configure age rules | Sys Admin (country pack defaults) |

## 7. Database concept

Entities: `Stage` (school, bilingual, ordered, gender policy default); `GradeLevel` (stage, code, bilingual, order, promotion-target ref, graduating flag); `GradeYearProfile` (grade × academic year: curriculum, gender policy, age rule, capacity plan, active flag) — the year-versioning vehicle (BR-GRD-008). Enrollment (Module 03 concept) references GradeYearProfile, freezing historical structure naturally.

## 8. Required screens

1. **Ladder builder** — stages and grades as an ordered tree; drag ordering; promotion-path arrows visualized; graduating flags.
2. Grade-year profile editor — per year: curriculum, gender, age rule, capacity (sections × size) with live seat math.
3. Capacity board — per grade: planned vs enrolled vs applications pending (Admissions feed), waiting-list depth.
4. Active-year change request — impact panel + P2 submission.

## 9. Validation rules

Unique grade codes per school; promotion path must be acyclic and complete (every non-graduating grade has a target — checked at year activation, BR-AYR-004); gender policy narrowing only (stage Mixed → grade Boys allowed; stage Boys → grade Mixed blocked); age min < max; capacity ≥ current enrollment when edited mid-year; curriculum change on a grade with published marks blocked for that year.

## 10. Reports

Ladder sheet (bilingual, per year) · Capacity vs enrollment by grade (utilization %) · Promotion-path map · Grade-year change register (audit view) · Age-exception register (BR-GRD-005 overrides).

## 11. Dashboard widgets

Principal: enrollment vs capacity heat bar per grade. Registrar: grades over/under capacity, age exceptions pending. Admissions: open seats by grade (live).

## 12. Notifications

`CapacityThresholdReached` (90% configurable) → Registrar, Admissions, Principal; `ActiveStructureChangeApproved` → Registrar, Sys Admin; `GradeFull` → Admissions (waiting-list mode trigger).

## 13. Future enhancements

Multi-curriculum transcript bridging (student switching tracks mid-ladder); ability-based placement layers (streaming/setting within a grade — relates to Sections future); capacity forecasting from Admissions pipeline + re-registration signals.

## 14. Open questions

1. Confirm dual-track modeling (parallel grades per curriculum, BR-GRD-003) against a real dual-curriculum prospect — alternative (curriculum per section) rejected for grading-scale coherence; revisit if a prospect contradicts.
2. Age cutoff dates per country pack (values needed for BR-GRD-005).
3. Do any target schools need mid-year gender-policy changes (assumed no — blocked without support intervention)?
