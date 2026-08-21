# 06 — Security Framework

**Phase:** 2 — Cross-cutting frameworks | **Status:** Draft for review | **Owner:** Security Architect

---

## 1. Principles

1. **Deny by default** (BR-GLB-070): no permission ⇒ no visibility — menus, screens, buttons, data, reports all disappear rather than error.
2. **Least privilege** via seeded role templates that schools adjust down, not up-from-admin.
3. **Segregation of duties**: requester ≠ approver (BR-WF-003); cashier ≠ discount approver; marks entry ≠ marks publication.
4. **Data scoping is part of authorization**, not report filtering: a scope-limited user *cannot* query outside their scope by any path.
5. Every security-relevant event is audited (doc 07).

## 2. Account model

| Account type | Who | Characteristics |
|--------------|-----|-----------------|
| Staff account | Employees | Username/email login; role-based; scoped; access to staff app |
| Parent account | Parents (one per parent entity) | Portal only; sees own children across years; one login even with children in multiple schools of a group (future) |
| Student account | Students (upper grades, school-configurable) | Portal only; own data only |
| System account | Integrations/jobs | Non-interactive, key-based, least-privilege, audited |

Rules: one person = one account (linked to the person entity, BR-GLB-002). Accounts are deactivated, never deleted (history integrity). Employee offboarding and student withdrawal automatically deactivate accounts (module workflows trigger it).

## 3. Authentication

| ID | Rule |
|----|------|
| BR-SEC-001 | Passwords: configurable policy per school with product minimums (length ≥ 10, complexity, history 5, max age optional); stored with a modern adaptive hash. |
| BR-SEC-002 | Lockout after N failed attempts (default 5) with timed unlock and audit; CAPTCHA on portal after 3 failures. |
| BR-SEC-003 | 2FA (TOTP or OTP via SMS/email) mandatory-capable per role; default ON for System Admin and Finance roles. |
| BR-SEC-004 | Sessions: idle timeout (default staff 30 min, portal 20 min), absolute timeout 12 h, single-session policy configurable for finance roles. |
| BR-SEC-005 | First-login forced password change; admin resets issue one-time passwords only; no password is ever visible or mailed in clear beyond the one-time flow. |
| BR-SEC-006 | Parent/student accounts are provisioned by the school (no self-signup in v1); activation via emailed/SMS'd one-time link. |

## 4. Authorization model

**Permission = (Module, Screen, Action) × Data Scope.**

### 4.1 Action taxonomy (standard verbs)

`View · Create · Edit · Deactivate · Submit · Approve · Post · Print · Export · Import · Configure`

Module docs may add verbs only if none of these fit (justified in the module doc). "Delete" exists only as Deactivate/Void per BR-GLB-005.

### 4.2 Data scopes

A role assignment carries scope dimensions; empty dimension = all within the lower bound granted:

| Dimension | Values | Example |
|-----------|--------|---------|
| School | one, many, all (group future) | Branch accountant: School A only |
| Academic Year | active only / active+preparation / all years | Teacher: active year; Registrar: all |
| Grade | subset | Stage supervisor: Grades 1–6 |
| Section | subset / "own sections" (dynamic) | Homeroom teacher: own sections |
| Own-records-only | flag | Teacher sees own timetable/assignments |

"Own sections / own subjects" scopes resolve dynamically from Teacher Assignments each year — no manual re-scoping at rollover.

### 4.3 Roles

- A **role** is a named set of permissions; users may hold multiple roles; effective permission = union of grants (no explicit-deny in v1 — simpler and auditable; explicit-deny listed as Future).
- **Seeded role templates** (adjustable per school): System Administrator, Principal, Vice Principal, Stage Supervisor, Registrar, Admissions Officer, Finance Manager, Cashier, HR Officer, Teacher, Homeroom Teacher, Head of Department, Nurse, Librarian, Storekeeper, Cafeteria Operator, Transport Supervisor, Receptionist, Parent, Student, Auditor (read-only + audit access).
- Role/permission **changes require the System Admin permission and are field-level audited**; granting the Configure-Security permission itself requires a second admin's approval (P2 workflow) — prevents silent privilege escalation.

## 5. Restricted data categories (BR-GLB-072)

| Category | Default access | Notes |
|----------|---------------|-------|
| Medical file | Nurse, Principal; emergency banner (allergies) visible to teachers of the student | Banner content is the configurable minimum, not the file |
| Discipline records | Discipline roles, Principal, homeroom teacher (own students) | Portal visibility per school policy |
| Financial hardship / scholarship reasons | Finance Manager, Principal | Excluded from general finance screens |
| Identity documents & attachments | Registrar, HR (staff docs) | Watermarked on print/export |
| Salary/contract data (HR) | HR Officer, Principal | Invisible to all other roles including IT admin screens |

## 6. Portal security

| ID | Rule |
|----|------|
| BR-SEC-010 | Portal accounts reach only portal areas; staff URLs return not-found (not access-denied) to portal sessions. |
| BR-SEC-011 | A parent sees exactly the students linked to them with an active, non-revoked guardianship link; custody restrictions (court orders) can revoke a specific parent's visibility per student — handled in Parent module. |
| BR-SEC-012 | Portal shows published data only (published results, posted invoices); drafts and internal notes are never portal-visible. |
| BR-SEC-013 | Payment and personal-data pages require re-authentication after 15 idle minutes. |

## 7. Administrative safeguards

| ID | Rule |
|----|------|
| BR-SEC-020 | Impersonation ("login as") is permission-gated, banner-flagged, fully audited, and blocked for finance-posting actions. |
| BR-SEC-021 | Bulk export of personal data requires the Export permission per screen and is audited with row counts (BR-GLB-074). |
| BR-SEC-022 | Security configuration reports: users-with-role, role-permission matrix, dormant accounts (no login > 60 days), scope exceptions — all available to Auditor role. |
| BR-SEC-023 | All traffic over TLS 1.2+; attachments served through permission-checked endpoints, never direct file URLs. |
| BR-SEC-024 | Personal data at rest: database-level encryption (TDE or equivalent) for cloud offering; on-prem documented as deployment requirement. |

## 8. Screens (System Administration module will own these)

Users list & lifecycle · Role designer (permission tree with action columns) · Role assignment with scope picker · Permission matrix report · Security audit viewer · Password/2FA policy settings · Session policy settings · Account provisioning batches (parents/students).

## 9. Future enhancements

Explicit-deny rules; SSO (Azure AD/Google Workspace) for staff; national SSO integrations; IP allow-listing for finance screens; fine-grained field-level permissions UI.

## 10. Open questions

1. Data-protection law specifics per confirmed country list (consent capture at admission, retention schedule) — needed before Student/Admissions modules (Phase 4).
2. Should student portal accounts be per-school-configurable by stage (e.g., Grade 7+)? Recommendation: yes, configurable minimum grade.
3. Auditor role: internal only, or will external auditors/ministry get time-boxed accounts?
