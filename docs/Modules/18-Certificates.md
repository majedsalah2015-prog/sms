# Module 18 — Certificates

**Phase:** 5 — Academic operations | **Status:** Draft for review | **Rule prefix:** `BR-CRT`

---

## 1. Purpose

The official-document issuance engine: template-driven, bilingual, numbered, approval-gated, verifiable certificates — academic (completion, transcripts, honor), administrative (enrollment proof, transfer certificate, conduct), and service letters — with a permanent issuance register per student.

## 2. Scope

**In:** certificate type catalog & templates (product layouts + branding/signatory slots per BR-SCH Q3 decision), issuance workflows (WF-09), numbering & QR verification, issuance register, reprints/copies policy, bulk issuance (graduation batches), portal request flow; employee service certificates (BR-EMP-008) reuse this engine.
**Out:** result computation (Module 17 supplies data), report cards (Module 17 owns generation; official copies are numbered via this module's engine), fee clearance rules (consumed from Module 19 policy).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-CRT-001 | **Certificate types** are cataloged: enrollment proof (شهادة قيد), transfer certificate (TC), completion/graduation certificate, transcript, conduct certificate (حسن سيرة وسلوك), honor certificate, custom letter types; each type defines: template, data sources, approval chain, prerequisites (e.g., TC requires WF-03 clearance; transcript requires published results), fee-clearance requirement (config per type per school), validity period (enrollment proofs expire), and portal-requestability. |
| BR-CRT-002 | Templates are **product-fixed layouts with slots** (branding, signatories per BR-SCH-004, dynamic fields) per Phase 1 decision; bilingual: single bilingual layout or per-language variants per type; all official prints carry: certificate number (doc 08 strict series), issue date (dual calendar per config), QR verification code, signatory block. |
| BR-CRT-003 | **Issuance = WF-09** (P2 default; type-configurable chain): request (staff or portal) → prerequisite auto-check (results published, clearance, fee position per type config) → approver → generate. Generation is atomic with numbering (BR-WF-009): the PDF is rendered, numbered, stored immutable (doc 10), and registered — no unnumbered official output exists. |
| BR-CRT-004 | **Data snapshot:** every certificate stores its rendered data snapshot (student identity, results, school identity per BR-SCH-002) — reprints reproduce the original exactly; current-data reissue is a **new certificate** (new number) with the old one optionally revoked. |
| BR-CRT-005 | **Verification:** QR/code resolves to a public verification endpoint returning: valid/revoked, type, student name (configurable disclosure level), issue date — no further personal data (privacy default); verification hits are logged. |
| BR-CRT-006 | **Revocation:** issued certificates can be revoked (error, fraud, replacement) with reason + P2 (Principal); revoked numbers remain in the register (BR-NUM-002) and verification returns Revoked. |
| BR-CRT-007 | **Reprints/copies:** original prints once; subsequent prints watermark "True Copy" with reprint count in register; copy fees per school policy (Module 19 misc-charge hook). |
| BR-CRT-008 | **Fee-clearance gate:** types flagged clearance-required check the student's financial position (Module 19) at approval; blocking rule per school config (full clearance / no overdue / disabled) with Principal override (T1 + reason) — legal caution: some jurisdictions restrict withholding academic documents for unpaid fees → country-pack flag governs which types may legally be gated (open question Q1). |
| BR-CRT-009 | **Bulk issuance:** graduation/completion batches per grade (all-prerequisites-met list, exceptions queue), single approval covering the enumerated batch, individual numbers per certificate. |
| BR-CRT-010 | Issuance register is permanent and T1-audited; certificate data snapshots retained beyond student retention purge in anonymized-register form per country pack (number, type, date survive; personal fields per law). |

## 4. Workflow

WF-09 per type chain (default P2: Registrar → Principal for academic types; Registrar-only for enrollment proofs — configurable). Portal requests: parent request → optional copy-fee payment → chain → notification + counter pickup or portal download (config per type). Revocation: P2 Principal. Bulk: batch approval (BR-CRT-009).

## 5. User roles

Registrar (owner, issuer), Principal (approver, revocations, overrides), Finance (clearance data, copy fees), HR (service certificates for employees via same engine), Parent/Student (portal requests, downloads), External verifier (public endpoint, no auth).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Configure types/templates | Sys Admin + Registrar |
| Request issuance | Registrar staff; Parent (portal-requestable types) |
| Approve issuance | Per type chain (WF-09) |
| Clearance override | Principal (T1) |
| Revoke | Principal (P2) |
| View register | Registrar, Principal, Auditor |
| Bulk issuance | Registrar + Principal |

## 7. Database concept

Entities: `CertificateType` (template ref, chain config, prerequisites, clearance flag, portal flag, validity); `CertificateIssue` (number, student/employee ref, type, data snapshot JSON, PDF ref, status: issued/revoked, verification code, reprint count); `CertificateRequest` (workflow-managed); `VerificationLog`. Report-card official copies (BR-GRA-008) and employee service letters register through `CertificateIssue` — one register for everything official.

## 8. Required screens

1. Type catalog & template configuration — slots preview with live sample render per language.
2. Issuance desk — student search → eligible types (prerequisite status inline) → request/approve → print/download; queue view for pending chain steps.
3. Certificate register —全 issued documents: filters (type, period, status), reprint action (watermarked), revoke flow.
4. Bulk issuance wizard — batch criteria, eligibility list with exceptions, approval, batch print.
5. Portal: request certificate (type list per config, fee step placeholder), my documents (issued downloads).
6. Public verification page — code entry / QR landing, disclosure per BR-CRT-005.

## 9. Validation rules

Prerequisites hard-checked at approval (results published for transcripts; WF-03 complete for TC; clearance per BR-CRT-008 config); signatory must be active for the document class (BR-SCH-004); template render must resolve all mandatory slots (missing data blocks with named fields); revocation reason mandatory; portal requests limited per period (anti-abuse config); validity dates printed where the type expires. |

## 10. Reports

Issuance register per period/type · Revocation register with reasons · Verification activity report (external checks per certificate — fraud signal) · Clearance-override register (T1 view) · Copy-fee revenue summary (Finance feed) · Graduation batch summaries · Pending requests aging (SLA per doc 05). |

## 11. Dashboard widgets

Registrar: pending requests queue, today's issuances. Principal: approvals pending, overrides this term. Portal: my requests status.

## 12. Notifications

`CertificateRequested` → approver chain (SLA); `CertificateReady` → parent (pickup/download); `CertificateRevoked` → Registrar, Principal (+ holder per policy); `ClearanceBlocked` → requester + Finance (with position summary); `VerificationAnomaly` (repeated failed codes) → Registrar. |

## 13. Future enhancements

Full template designer (school-designed layouts — Future per BR-SCH Q3); digital signatures (PKI) and national e-document integration per country; blockchain-anchored verification (market-driven); apostille/attestation tracking workflow; alumni self-service transcript requests (with Alumni module).

## 14. Open questions

1. **Legal review per country: which document types may be withheld for unpaid fees** (BR-CRT-008)? Known regulatory sensitivity (e.g., ministries often prohibit withholding TCs) — country-pack legal input required before defaults ship. |
2. Verification disclosure level default (name shown vs initials vs none)? Proposed: full name + type + date (matches paper practice) — confirm privacy counsel per pack. |
3. Copy fees: flat per type via misc-charge (proposed) — confirm schools actually charge (else drop the hook). |
4. Are ministry-stamped physical forms (pre-printed government stock) required for TCs anywhere in target markets? If yes: print-onto-form calibration support needed (layout offsets) — scope check. |
