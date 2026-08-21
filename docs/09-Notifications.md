# 09 — Notifications Framework

**Phase:** 2 — Cross-cutting frameworks | **Status:** Draft for review | **Owner:** Software Architect + Senior Business Analyst

> Scope note: this is the **system-generated notification engine**. Person-to-person communication (teacher ↔ parent threads, announcements) is the **Messaging module (32)**; module 33 (Notifications) will own the *administration screens* of this engine. This document defines the shared framework both build on.

---

## 1. Purpose

Turn business events (absence recorded, results published, payment received, installment due) into timely, bilingual, multi-channel notifications — template-driven, school-configurable, fully logged — without any module implementing its own sending logic.

## 2. Model

```
Business Event  →  Subscription Rules  →  Recipient Resolution  →  Template (per language)  →  Channel(s)  →  Delivery Log
```

| Concept | Definition |
|---------|------------|
| Event | Typed fact raised by a module with payload (e.g., `Attendance.StudentAbsent` {student, date, section}) |
| Subscription rule | School config: which events notify whom, on which channels, with which template and timing (immediate/digest) |
| Recipient resolution | Roles resolved to people via scopes (doc 06); "parents of the student" resolved via active guardianship links honoring custody restrictions (BR-SEC-011) |
| Template | Bilingual (Ar/En) per channel, with typed placeholders validated against the event payload |
| Channel | In-app (bell + list), Email, SMS, WhatsApp Business (adapter-ready), Portal push (web) |
| Delivery log | Per recipient per channel: queued → sent → delivered/failed (+ provider reference, retries) |

## 3. Standard event catalog (v1 — extended by module docs)

| Module | Events (notify → default recipients) |
|--------|--------------------------------------|
| Admissions | ApplicationReceived → applicant parent; DecisionMade → parent; DocumentsMissing → parent |
| Students | StudentRegistered → parent; WithdrawalCompleted → parent, finance |
| Attendance | StudentAbsent (same day) → parents; RepeatedAbsence (threshold) → parents + homeroom + supervisor; LateArrival → parents (configurable) |
| Examinations/Grading | ExamScheduplePublished → parents, students; ResultsPublished → parents, students; MarkChangedAfterPublication → parents (mandatory, WF-08) |
| Certificates | CertificateIssued → parent |
| Fees/Payments | InvoicePosted → parent; InstallmentDueSoon (D-7, D-1 configurable) → parent; InstallmentOverdue → parent + finance; PaymentReceived (receipt) → parent; RefundProcessed → parent |
| Discipline | IncidentRecorded (severity-gated) → parents; ActionApplied → parents + homeroom |
| Health | ClinicVisit → parents; VaccinationDue → nurse, parents; MedicationAdministered → parents |
| Transportation | RouteAssigned → parent; BusDelayed (manual trigger) → route parents; StudentNotBoarded → parents (immediate) |
| Library/Store/Cafeteria | ItemOverdue → parent/student; LowBalance (cafeteria account) → parent |
| Employees | LeaveDecision → employee; ContractExpiring → HR; DocumentExpiring (Iqama/passport) → HR + employee |
| Workflow (doc 05) | StepAssigned/Overdue/Escalated/Approved/Rejected/Returned → actors |
| System | BackupFailed → IT admin; JobFailed → IT admin; PasswordChanged/2FA changed → account owner |

## 4. Business rules

| ID | Rule |
|----|------|
| BR-NOT-001 | Templates are bilingual; the recipient's preferred language (per account) selects the variant; missing-variant falls back to school default language and is flagged in the admin screen (BR-GLB-100). |
| BR-NOT-002 | Events fire only on **committed** business facts — never from drafts (BR-GLB-031); result notifications only on publication, invoice notifications only on posting. |
| BR-NOT-003 | Channel routing, timing, and enable/disable are school configuration per event; product defaults ship enabled for the catalog above (in-app + email; SMS for absence, overdue, OTP). |
| BR-NOT-004 | Quiet hours per school (default 20:00–07:00 school TZ): non-urgent notifications are held; urgent classes (safety: StudentNotBoarded, clinic emergency) bypass quiet hours. |
| BR-NOT-005 | Digest support: high-volume events (overdue reminders) can batch into one daily message per parent instead of per-child spam. |
| BR-NOT-006 | Every send is logged per recipient/channel with delivery status; failures retry with backoff (3 attempts) and surface in the admin dashboard; SMS/WhatsApp cost counters are tracked per school. |
| BR-NOT-007 | Recipients cannot opt out of statutory/safety classes (fees legal notices per school policy, safety events); other classes are parent-preference opt-out-able via portal. |
| BR-NOT-008 | All notification content is retained and viewable per student/parent file (communication history, BR-GLB-102); content sent is snapshotted (later template edits don't rewrite history). |
| BR-NOT-009 | Providers (SMS/email/WhatsApp gateways) are pluggable adapters with per-school credentials; provider failure never blocks the originating business transaction (queue decouples). |
| BR-NOT-010 | Placeholder data respects security: no marks in SMS bodies by default (link to portal instead) — configurable, with the restrictive default. |

## 5. Screens (owned by module 33)

Notification center (per user: bell, list, mark-read, filters) · Event–subscription configuration matrix (event × channel × recipients × timing) · Template editor (bilingual, placeholder picker, live preview, test-send) · Delivery log explorer (status, retries, provider refs) · Provider settings (gateways, credentials, sender IDs) · Quiet-hours & digest settings.

## 6. Reports & widgets

Delivery success rate by channel/provider · SMS/WhatsApp usage & cost per school per month · Undeliverable-contact report (bounced emails, dead numbers → data-quality queue for Registrar) · Notification volume by event type · Widget: failed deliveries today (IT admin dashboard).

## 7. Non-functional

Result-publication day is the peak: notifying 5,000 students' parents within 15 minutes (queued, throttled per provider limits). Delivery logging must not block business transactions (BR-NOT-009).

## 8. Future enhancements

Native mobile push; parent-preferred-channel selection per event class; WhatsApp interactive templates (approval buttons); scheduled announcements with audience builder (with Messaging module).

## 9. Open questions

1. WhatsApp Business API availability/approval per target country — determines whether v1 ships the adapter enabled or SMS-first.
2. Are fee-overdue notifications legally sensitive in any target country (formal notice requirements)? Affects template wording per country pack.
3. Default SMS budget guardrails: hard monthly cap per school with alert, or alert-only? Recommendation: alert at 80%, hard-stop optional per school.
