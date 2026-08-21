# Module 09 — Admissions

**Phase:** 4 — People | **Status:** Draft for review | **Rule prefix:** `BR-ADM`

---

## 1. Purpose

Run the applicant pipeline — inquiry/application → review → decision → registration — ending with an enrolled student carrying a generated student number, a section seat, generated fees, and a linked (deduplicated) parent, per WF-01.

## 2. Scope

**In:** admission campaigns per academic year, application capture (counter + portal public form), document checklist, entrance assessment (optional), seat management against grade capacity, waiting lists, sibling detection, decision workflow, registration conversion, mid-year admissions, transfer-in students (incoming TC), application fees (optional).
**Out:** re-registration of existing students (Module 03 rollover), fee structure definition (Module 19 — consumed here), parent entity rules (Module 11 — consumed here), student file (Module 10 — created here).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-ADM-001 | Applications belong to an **admission campaign** (school × academic year × optional intake window); campaigns define target grades, open/close dates, required documents (doc 10 checklist), assessment requirement, and application fee policy. |
| BR-ADM-002 | An application is for one applicant × one grade × one campaign; duplicate live applications for the same applicant (ID-matched per BR-GLB-003) are blocked with a link to the existing one. |
| BR-ADM-003 | Application capture always runs **parent deduplication first** (BR-GLB-004): match by ID/phone → link existing parent (with their other children visible = automatic **sibling detection**) or create new. Sibling flags surface on review (priority policy configurable) and feed sibling-discount eligibility (Module 22). |
| BR-ADM-004 | Age eligibility validates per BR-GRD-005 at capture (override permission-gated, logged); grade seat availability checks against BR-GRD-006 capacity: full grades accept to **waiting list** only. |
| BR-ADM-005 | Workflow (WF-01, P3+P4): `Draft → Submitted → Under Review → [Assessment] → Recommended → Approved/Rejected/Waitlisted`. Review = document verification + optional assessment score entry; approval authority: Admissions Officer recommends, Registrar approves, Principal approves exceptions (over-capacity, age override, fee exception). Rejection requires reason (BR-WF-010); portal shows simplified states. |
| BR-ADM-006 | **Waiting list** is ordered per grade by configurable policy (submission time, sibling priority, assessment score); when a seat frees (withdrawal, capacity increase, unclaimed re-registration seats after deadline — BR-AYR §4), the top entry is offered with an expiry window; expired offers pass to the next. All offers/expiries logged. |
| BR-ADM-007 | **Registration** (post-approval, one transaction — BR-WF-009): student record created (or reactivated for returning students, keeping their original number per BR-NUM-004), **student number generated**, enrollment + section assignment created (Module 06 rules), **fees generated** from the grade's fee structure incl. pro-rating for mid-year entry (Module 19 policy), portal accounts provisioned per policy, mandatory documents re-verified. An approved application not registered by the configured deadline lapses (seat released). |
| BR-ADM-008 | Application fee (if campaign defines one): payable at submission (receipt via Module 21, strict numbering), refundability per campaign policy; unpaid applications cannot advance past Submitted (configurable). |
| BR-ADM-009 | Transfer-in students: incoming transfer certificate + prior-school records are checklist documents; prior academic history capturable into the student file (Module 10 academic history, source-flagged External). |
| BR-ADM-010 | Applicant personal data captured before approval is subject to data-protection consent (country pack): the application form carries consent capture; rejected/lapsed application data follows the retention schedule (BR-AUD-006 alignment), then purges per BR-ATT-011. |
| BR-ADM-011 | Applications are T2-audited; decisions and overrides T1 with reasons. |

## 4. Workflow

WF-01 as refined in BR-ADM-005/007; visualized:

`Inquiry (optional CRM-lite) → Application (Draft/Submitted) → Document check → [Assessment] → Review → Decision (Approve / Reject / Waitlist) → Offer (waitlist path) → Registration → Enrolled student`

Mid-year admissions use the same pipeline with pro-rated fees; escalations per doc 05 SLAs (default: review ≤ 5 working days).

## 5. User roles

Admissions Officer (owner), Registrar (approver + registration), Principal (exception approver), Finance (fee exception input, application-fee receipts via cashier), Assessment staff (score entry only), Parent (portal applicant).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Capture/edit applications | Admissions Officer; Parent (portal, own, until Submitted) |
| Verify documents | Admissions Officer |
| Enter assessment scores | Assessment staff (scores only) |
| Approve / reject / waitlist | Registrar; exceptions Principal |
| Manage waiting list / offers | Admissions Officer (order changes audited T1) |
| Register (convert) | Registrar |
| View pipeline analytics | Principal, Registrar, Admissions |

## 7. Database concept

Entities: `AdmissionCampaign` (year, windows, grades, policies); `Application` (applicant person data pre-student, grade, status, source, consent, sibling links via parent ref); `ApplicationAssessment` (scores per configured criteria); `WaitingListEntry` (grade, order, offer state, expiry); `ApplicationDecision` (workflow trail via doc 05 engine). Registration creates/links: `Student`, `Enrollment`, `SectionMembership`, fee documents, portal accounts. Applicant → Student conversion preserves the application reference (application number ≠ student number, doc 08 Q3 decision).

## 8. Required screens

1. Campaign setup — windows, grades, documents, assessment, fees, waitlist policy.
2. **Public/portal application form** — bilingual, mobile-friendly, parent dedup step, document upload, consent, fee payment handoff (v1: pay-at-school note or gateway-ready), submission tracking.
3. Counter application capture — staff-speed form with dedup + sibling panel.
4. **Pipeline board** — kanban by status per campaign/grade, aging & SLA flags, filters (sibling, assessment band). |
5. Application detail — tabs: applicant, family (dedup links), documents checklist, assessment, decision history.
6. Waiting list manager — ordered per grade, offer/expiry actions, audit trail.
7. Registration wizard — final checklist (docs verified, section pick, fee preview incl. pro-ration and sibling discount eligibility), single-confirm conversion.

## 9. Validation rules

Mandatory applicant fields per campaign config (bilingual names, DOB, gender, nationality, ID per country pack); age vs BR-GRD-005; live-duplicate block (BR-ADM-002); document checklist gate at approval not upload (BR-ATT-006); assessment scores within configured ranges; registration blocked without: approval, verified mandatory docs, section with seat (or override), parent link; consent checkbox mandatory on portal submissions.

## 10. Reports

Funnel report (inquiries→applications→approved→registered, conversion % per campaign/grade) · Applications by status/aging (SLA) · Waiting list depth & offer outcomes per grade · Assessment score distribution · Rejection reasons analysis · Sibling-application report · Mid-year admissions register · Source-of-application analysis (marketing) · Lapsed approvals report.

## 11. Dashboard widgets

Admissions: pipeline counts by stage, today's assessments, offers expiring soon. Registrar: pending approvals, registrations this week. Principal: seats filled vs capacity per grade, conversion vs last year.

## 12. Notifications

`ApplicationReceived` → parent (ack + tracking ref); `DocumentsMissing` → parent; `AssessmentScheduled` → parent; `DecisionMade` → parent (approved: registration steps; waitlisted: position policy text); `OfferExtended`/`OfferExpiring (D-2)` → parent; `RegistrationCompleted` → parent (welcome pack: portal credentials flow, fee summary); `SLAOverdue` → Registrar (doc 05).

## 13. Future enhancements

Online application-fee payment (activates with gateway, Module 21); CRM-lite inquiry nurturing (visits, follow-ups); entrance-exam scheduling with capacity slots; e-signature on enrollment contract; ML seat-demand forecasting.

## 14. Open questions

1. Is an **enrollment contract** (signed terms incl. fee policy) legally required at registration in target countries? If yes: checklist document + version tracking — country pack input (README Q3). |
2. Waitlist priority default order (proposed: sibling → submission time) — confirm; assessment-ranked only where assessments exist. |
3. Application fee: campaign-level flag proposed — do any target schools waive for siblings (policy hook exists via Module 22)? |
4. Inquiry stage (pre-application CRM-lite) in v1 or Future? Recommendation: minimal inquiry log in v1 (name, phone, grade, source, follow-up note) — it feeds the funnel report; full CRM to Future. |
