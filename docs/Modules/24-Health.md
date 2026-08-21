# Module 24 — Health

**Phase:** 7 — Student services | **Status:** Draft for review | **Rule prefix:** `BR-HLT`

---

## 1. Purpose

The school clinic's system: the confidential student medical file (allergies, chronic conditions, medications, vaccinations), clinic visit records, medication administration control, screening campaigns, and the minimal emergency information teachers must see — under the strictest privacy tier in the product.

## 2. Scope

**In:** medical file per student (🔒 restricted category), emergency banner policy (the teacher-visible minimum), vaccination records & due tracking (country schedule packs), clinic visits (triage → outcome), medication-at-school authorizations & administration log, chronic-condition care plans, screening campaigns (vision/dental/BMI), infectious-disease absence linkage (Module 14 medical leave), clinic inventory (light), staff health records pointer (kept in Module 12 🔒, not here).
**Out:** insurance claim processing (Future), external medical integrations, counseling records (see Q3 — potentially even more restricted).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-HLT-001 | The medical file is **restricted (T0 read-audited)**: Nurse + Principal by default (BR-GLB-072); parents see their child's file via portal (their legal right); guardians with custody restrictions follow BR-SEC-011. All entries attributable to the recording nurse. |
| BR-HLT-002 | **Emergency banner:** a configurable minimal subset (severe allergies, critical conditions, emergency instructions) displayed to teachers/supervisors of the student (roster badges, attendance sheet icon, trip manifests); banner content is nurse-curated per student (not auto-extracted), parent-visible, and its display points are fixed by product (no casual browsing path). |
| BR-HLT-003 | **Intake:** medical questionnaire at admission (BR-ADM checklist option) parent-declared, nurse-verified; annual re-confirmation at re-registration (parents confirm/update — keeps files current, a known weak point in schools). |
| BR-HLT-004 | **Vaccinations:** records against the country-pack schedule; due/overdue computed per student age; school-administered campaigns (where legal) require per-campaign parent consent (portal consent capture, doc 10 stored); external records uploadable (vaccination card). |
| BR-HLT-005 | **Clinic visits:** numbered (doc 08), with reason, triage notes, vitals, outcome (`Returned to class / Sent home / Referred / Emergency`); Sent-home requires authorized-pickup verification (BR-PAR-008) + parent notification; Emergency triggers the school emergency protocol notification (urgent class); visit during a period auto-notifies the session teacher (student whereabouts). |
| BR-HLT-006 | **Medication at school:** only against a parent authorization (+ physician note per policy) with dosage/schedule; administration events logged (dose, time, nurse) against the authorization; missed/refused doses recorded; controlled storage list maintained; parents notified per administration (config per BR-NOT). |
| BR-HLT-007 | **Chronic care plans** (asthma, diabetes, epilepsy…): structured plan (triggers, response steps, emergency contacts) linked to the emergency banner; annual review flag. |
| BR-HLT-008 | **Screenings:** campaign per grade/section with per-student results (structured fields per screening type); abnormal findings → parent referral letters (Module 18 pattern) + follow-up tracker; aggregate stats anonymized for reports. |
| BR-HLT-009 | Infectious-disease cases can mark expected-absence windows feeding Module 14 (medical leave pre-approved) and trigger exposure notices to a section's parents (anonymized, Principal-approved send). |
| BR-HLT-010 | Medical data retention per country pack (longer than general records typically); T1 on file changes; visit/administration logs append-only in effect. |

## 4. Workflow

Visits: P1 direct (speed) with outcome gates (sent-home verification). Medication authorization: parent submit → nurse verify (P2). Campaigns (vaccination/screening): plan → Principal approve → consent collection → execution → results/referrals. Exposure notice: nurse draft → Principal approve (P2) → send.

## 5. User roles

Nurse (owner), Principal (approvals, access), Teachers (banner-only), Attendance Supervisor (sent-home coordination), Parents (portal: own child file, consents, notifications), Registrar (intake checklist linkage), Auditor (access logs only, not content — see Q4).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Full medical file | Nurse; Principal (read) |
| Emergency banner view | Student's teachers/supervisors (auto-scope) |
| Record visits/administration | Nurse |
| Approve campaigns/notices | Principal |
| Portal file view + consents | Parent (own child) |
| Access-log review | Auditor (T0 logs) |

## 7. Database concept

Entities: `MedicalFile` (conditions, allergies with severity, blood type, banner-config); `CarePlan`; `VaccinationRecord` (+ schedule pack refs); `ClinicVisit` (numbered, outcome); `MedicationAuthorization` + `AdministrationLog`; `ScreeningCampaign` + `ScreeningResult`; `ConsentRecord` (campaign × student); `ExposureNotice`. All 🔒 flagged; banner subset denormalized for scoped fast display without opening the file. |

## 8. Required screens

1. **Clinic desk** — student search → banner + file → new visit (fast triage form) → outcome actions (pickup verification embedded).
2. Medical file editor — conditions/allergies/plans, banner curation preview ("what teachers see").
3. Medication board — today's due administrations checklist, authorization manager.
4. Vaccination tracker — per student & campaign views, due lists per section.
5. Screening campaign wizard — cohort, consent status board, result entry grids, referral batch.
6. Portal: child health summary, questionnaire/re-confirmation, consents, visit notifications history.

## 9. Validation rules

Visit outcome mandatory; sent-home requires verified pickup or documented exception; administration only within authorization dosage/schedule window (deviation requires reason, flagged); allergy severity mandatory; campaign execution only for consented students (hard); banner edits show teacher-view preview before save; questionnaire re-confirmation tracked yearly (nag at re-registration). |

## 10. Reports

Clinic visit register (by period/reason/outcome) · Frequent-visitor report (pattern flag → welfare signal) · Vaccination compliance per grade (ministry format per pack) · Medication administration log 🔒 · Screening outcomes (anonymized aggregates + referral follow-up status) · Allergy/chronic register for emergency planning 🔒 (kitchen/trip planning feed — see Cafeteria linkage) · Access audit report (who viewed medical data — T0). |

## 11. Dashboard widgets

Nurse: today's medications due, open follow-ups, visits today. Principal: clinic volume trend, vaccination compliance %. Teacher (banner only): alert icons on rosters. Portal: upcoming vaccinations, consents pending.

## 12. Notifications

`ClinicVisit` → parents (same day; Emergency = urgent class); `SentHome` → parents urgent + supervisor; `MedicationAdministered` → parents (config); `VaccinationDue` → parents, nurse; `ConsentRequested` → parents; `ScreeningReferral` → parents (formal letter); `ExposureNotice` → section parents (anonymized, approved). |

## 13. Future enhancements

Insurance policy tracking & claim export; ministry health-platform integration per country; counseling/wellbeing module (separate ultra-restricted tier); allergy-aware cafeteria linkage (block flagged allergens at POS — coordinate Module 27 Q); wearable/emergency-alert integrations.

## 14. Open questions

1. School-administered vaccination legality per country (vs record-only) — pack input required (BR-HLT-004). |
2. BMI/growth screening sensitivity: include in v1 screenings (proposed yes, results parent-visible only)? |
3. Counseling notes: explicitly **out of v1** (needs its own governance tier) — confirm. |
4. Auditor access: logs-only (proposed) vs content access with court-order flag — confirm privacy stance. |
5. Cafeteria allergen linkage (block sale of flagged allergens): v1 or Future? Cheap if Module 27 POS knows the flag — **recommend v1 as warning-level**, hard-block Future. |
