# Module 02 — Schools

**Phase:** 3 — Academic structure | **Status:** Draft for review | **Rule prefix:** `BR-SCH`

---

## 1. Purpose

Define the School entity — the tenant scope of all data (ADR-2) — with its official identity, branding, structure, and officials, serving one school today and school groups tomorrow without schema change.

## 2. Scope

**In:** school profile (bilingual names, license, ministry codes, contacts, address, geo), branding assets (logo, seal, header/footer for documents), stages offered, official signatories (for certificates/reports), school status lifecycle, group placeholder (future).
**Out:** settings values (Module 01), academic years (Module 03), physical rooms (Module 08).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-SCH-001 | School official names (Ar/En), license number, and ministry code are mandatory before activation; these appear on official documents exactly as entered (certificates, receipts). |
| BR-SCH-002 | School identity fields (names, license, ministry code) are T1-audited; changes require reason (they alter what official documents display). Issued documents are never retro-changed — they snapshot school identity at issuance (aligns BR-GLB-062 pattern). |
| BR-SCH-003 | A school declares its **stages offered** (from the stage structure, Module 05); grades outside offered stages cannot be created. |
| BR-SCH-004 | Official signatories are configured per document class (certificate → Principal name+title Ar/En, financial → Finance Manager); signatory changes are effective-dated so reissued documents remain faithful. |
| BR-SCH-005 | School status: `Setup → Active → Suspended → Closed`. Suspended blocks portal and transactions but preserves access for staff read + finance collection (configurable); Closed is terminal, read-only, retained per country pack. |
| BR-SCH-006 | Branding assets (logo, seal) are attachments (doc 10) with format/size constraints; every official template references current branding at render time except issued-immutable documents (BR-SCH-002). |
| BR-SCH-007 | Multi-school future: a `SchoolGroup` concept exists from v1 as an optional parent reference; v1 UI shows it only when > 1 school exists. No consolidation logic in v1 (README Q2 decision). |
| BR-SCH-008 | Contact channels (official email, phone, website, map location) are validated and used as sender identities in notifications (doc 09 provider settings). |

## 4. Workflow

School activation is checklist-gated by the Setup Wizard (BR-SET-003): `Setup → Active` requires wizard completion; `Active → Suspended` and reactivation are Sys Admin + Product Support actions with reason (T1); `→ Closed` is product-support-only (contract end).

## 5. User roles

System Administrator (manage), Principal (view + signatories), Product Support (status transitions), all staff (implicit — school context display in shell).

## 6. Permissions

| Action | Roles |
|--------|-------|
| View school profile | Sys Admin, Principal, Auditor |
| Edit profile/branding | Sys Admin |
| Edit signatories | Sys Admin + Principal approval (P2) |
| Change status | Product Support |
| View group (future) | Group roles (future) |

## 7. Database concept

Entities: `School` (bilingual names, license, ministry code, address incl. geo, contacts, status, TZ/currency refs, optional GroupId); `SchoolGroup` (future-activated); `Signatory` (document class, name Ar/En, title Ar/En, effective dates); branding via attachment references. School is the root of all SchoolId scoping — every tenant table FKs to it (Phase 10 formalizes).

## 8. Required screens

1. **School profile** — tabbed: Identity (Ar/En side-by-side), License & Ministry, Contacts & Location (map pin), Branding (logo/seal upload + document preview), Stages offered.
2. **Signatories** — per document class with effective dating and history.
3. **School status** — support console with reason capture.
4. *(Future)* Group tree view.

## 9. Validation rules

License number format per country pack; ministry code mandatory when the pack defines statutory exports; logo formats PNG/SVG ≤ 2 MB with transparent-background recommendation and live preview on certificate/receipt templates; geo coordinates optional but validated; email/phone format checks; stage removal blocked if grades exist under it.

## 10. Reports

School profile sheet (bilingual, for ministry/partners) · Signatory history · Status change log (audit view).

## 11. Dashboard widgets

None school-facing (identity module); Product/support dashboard (future multi-tenant ops): schools by status, license expiry.

## 12. Notifications

`SignatoryChanged` → Principal, Sys Admin; `SchoolStatusChanged` → Principal, Product Support; `LicenseExpiring` (if license end date captured) → Principal, Product Support (D-90/D-30).

## 13. Future enhancements

Multi-school group operations: consolidated dashboards, cross-school student transfer workflow, group-level role scopes (doc 06 already dimensions this), shared parent accounts across group schools (anticipated by BR-GLB-002/004).

## 14. Open questions

1. Does the school license (ministry permit) have an expiry the product should track (BR-ATT-008 candidate)? Assumed yes, optional field.
2. Are branch campuses of one license one School or many? Recommendation: **many Schools in one group** (cleaner scoping); confirm with a real prospect case.
3. Should receipt/certificate templates be school-designable in v1 (template designer) or product-fixed layouts with branding slots? Recommendation: fixed layouts + branding/signatory slots in v1; designer in Future (feeds Modules 18/21 and Phase 9).
