# 10 — Attachments Framework

**Phase:** 2 — Cross-cutting frameworks | **Status:** Draft for review | **Owner:** Software Architect + Security Architect

---

## 1. Purpose

One central, permission-aware document store used by every module: student documents, admission papers, medical reports, employee contracts, certificates, receipts scans — typed, size-controlled, expiring-document-aware, and never orphaned from its owning record.

## 2. Model

| Concept | Definition |
|---------|------------|
| Attachment | Stored file + metadata: document type, owning entity (any module), uploader, UTC timestamp, size, format, bilingual title/notes, status |
| Document type | Taxonomy entry per module (e.g., Birth Certificate, Iqama, Vaccination Card, Contract, Qualification, Medical Report) with rules: allowed formats, max size, mandatory-for (workflow stage), expiry-tracked flag, restricted-category flag |
| Document checklist | Per workflow (e.g., admission): required document types with per-item status (missing / uploaded / verified / expired) |
| Verification | Staff action marking a document as sighted-and-valid (verifier + date audited) |
| Expiry tracking | Types flagged expiry-tracked carry an expiry date; engine raises DocumentExpiring events (doc 09) |
| Version | Re-upload creates a new version; prior versions retained (permission-gated view); "current" pointer moves |

## 3. Business rules

| ID | Rule |
|----|------|
| BR-ATT-001 | Every attachment has a document type; untyped uploads are impossible (a generic "Other" type exists per module, discouraged and reportable). |
| BR-ATT-002 | Allowed formats default: PDF, JPG, PNG (documents); +DOCX/XLSX where a module justifies it. Executables and scripts are rejected by content inspection, not extension alone. |
| BR-ATT-003 | Size limits: default 10 MB per file, configurable per document type within a product ceiling (25 MB); per-entity total quota configurable. |
| BR-ATT-004 | Access control inherits from the owning entity **and** applies the document type's restricted-category flag (BR-GLB-072, BR-GLB-090): a user who sees the student but lacks medical access cannot see medical attachments. |
| BR-ATT-005 | Files are served only via permission-checked endpoints (BR-SEC-023); restricted-category views are T0-audited (doc 07). |
| BR-ATT-006 | Mandatory-document rules block the owning workflow's approval step, not the upload stage (BR-GLB-091): an admission can be drafted incomplete but not approved incomplete — override only by permission, logged with reason. |
| BR-ATT-007 | Attachments on transacted records follow soft-delete rules: they are voidable (with reason, audited), never physically removed while the owning record exists; physical purge only via the retention process. |
| BR-ATT-008 | Expiry-tracked documents (Iqama, passport, contracts, driver licenses, bus registrations) require an expiry date at upload; expiring items surface in module dashboards and raise notifications at configured lead times. |
| BR-ATT-009 | Virus scanning is a mandatory pipeline hook (cloud: scanning service; on-prem: ICAP/CLI adapter); files are quarantined until scanned, and never downloadable from quarantine. |
| BR-ATT-010 | Storage is abstracted (disk / blob per deployment, T-7); the database stores metadata + content reference, never file bytes; references include content hash for integrity checks. |
| BR-ATT-011 | Retention & purge: when a person's data passes its retention period (country pack, BR-AUD-006 alignment), attachments are purged with a logged purge certificate (what, when, by which policy) — the audit trail of the purge outlives the files. |
| BR-ATT-012 | Bulk download/export of attachments is a distinct permission, audited with counts (aligns BR-SEC-021). |

## 4. Standard document-type catalog (starter set; modules extend)

| Module | Types (● = mandatory by default, ⏰ = expiry-tracked, 🔒 = restricted) |
|--------|--------------------------------------------------------------------|
| Admissions/Students | ● Birth certificate · ● National ID/Iqama ⏰ · Passport ⏰ · ● Previous school report · Transfer certificate (incoming) · ● Photo · Custody/court order 🔒 |
| Parents | National ID/Iqama ⏰ · Proof of guardianship 🔒 |
| Health | Vaccination card 🔒 · Medical reports 🔒 · Allergy documentation 🔒 · Medication authorization 🔒 |
| Employees | ● Contract ⏰ 🔒 · ● Qualifications · ● ID/Iqama ⏰ · Work permit ⏰ · Training certificates · Medical fitness 🔒 |
| Transportation | Driver license ⏰ · Vehicle registration ⏰ · Insurance ⏰ |
| Finance | Payment proof scans · Sponsorship/scholarship letters 🔒 · Bank transfer evidence |
| Discipline | Incident evidence 🔒 · Signed pledges 🔒 |
| Certificates | Issued certificate PDF (system-generated, immutable) |

## 5. Screens

Upload widget (drag-drop, type picker, camera capture on mobile, bilingual title) — embedded in every owning screen · Document checklist panel (workflow screens) · Entity documents tab (typed list, versions, verify action, void) · Expiring documents console (cross-module, filter by module/type/days) · Document-type administration (taxonomy, rules) · Quarantine console (IT admin).

## 6. Reports & widgets

Missing mandatory documents by student/section/grade (Registrar) · Expiring documents (30/60/90 days) by module (HR, Transport, Registrar) · Storage usage per school/module (IT admin) · Unverified documents aging · Widget: "Documents expiring in 30 days" per relevant dashboard.

## 7. Non-functional

Upload P95 ≤ 5 s for 10 MB on school broadband; viewer streams (no full-download for preview); storage growth estimate (≈ 15 documents × 0.5 MB × student + staff files) feeds the Backup module sizing; content hash verified on restore tests (module 35).

## 8. Future enhancements

OCR + auto-classification of uploaded IDs; e-signature on pledges/contracts; parent self-upload via portal with verification queue (recommended early — reduces registrar load; module docs will scope it for re-registration v1.x); direct scanner integration.

## 9. Open questions

1. Photo policy: is the student photo mandatory and printable on reports/ID cards per school? Consent implications per country pack (README Q3).
2. Parent self-upload in v1 (portal) — recommended for re-registration season; confirm scope appetite.
3. On-prem virus scanning: require customer-provided scanner, or bundle one? Recommendation: pluggable adapter + documented requirement.
