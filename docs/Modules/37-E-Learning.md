# Module 37 — E-Learning

**Phase:** 9 — Roadmap R3 | **Status:** 📝 **Draft — scope opened 2026-08-30, NOT approved** | **Rule prefix:** `BR-LRN`

> **Scope-change notice.** Analysis v1.0 is approved and closed, and this module is *outside* it:
> decision **Q8** (`docs/README.md`) kept LMS features out of v1, and the gap register carries it as
> **G2 — "the largest functional gap"** with disposition *"R3 module or partner integration; build-or-partner
> decision by end of R1"* (`docs/Future/01-GAP-Analysis.md`). **That build-or-partner decision has not been
> taken.** This document opens the scope at the owner's instruction; it does not record an approval, and it
> does not amend the GAP register, the roadmap or the Vision — those remain owner-level edits pending sign-off.
> Extent chosen by the owner: the **full suite including online exams** (the largest of the four R3 options).

---

## 1. Purpose

Deliver teaching material, homework and online assessment on top of the existing academic spine — anchored on `CurriculumOffering`, marked into Module 17's marksheets, surfaced through the portal — **without becoming a second source of truth for a mark**. The design center is the boundary: this module owns *delivery and capture*; Module 16 keeps exam logistics and Module 17 keeps the calculation truth. An LMS that quietly re-computes a grade is how a school ends up with two report cards that disagree.

## 2. Scope

**In:** lesson plans and content per offering (optionally bound to a dated timetable `Session`), a versioned resource library, homework issue and portal submission, submission marking and feedback, question banks per offering, blueprint-reconciled paper generation, timed online sitting delivery (resumable, server-authoritative), auto-marking of objective items, a manual marking queue for constructed responses, lateness and integrity flags, raw-mark handoff into Module 17's marksheet mechanism, and **the first write surface the portal has ever had**.

**Out:** mark calculation, scales, GPA, ranking, report cards, transcripts, appeals (Module 17); exam logistics — schedule, room and seating allocation, invigilation roster, exam-day attendance and incident log (Module 16); certificates (Module 18); discipline cases arising from integrity flags (Module 25 owns the case); OMR bubble-sheet scanning (deferred — see §13); live/virtual classrooms and video conferencing (out of product); SCORM/xAPI package import (§13).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-LRN-001 | **Anchor:** all content, homework and papers reference `CurriculumOffering`, never raw `Subject` (BR-SUB-002/005 — year-correctness by construction). A lesson may additionally bind to a dated `Session` (Module 15): unbound it is a syllabus entry, bound it is "what happened that period". An end-dated offering (BR-SUB-004) keeps its content readable — content is never orphaned by a curriculum change. |
| BR-LRN-002 | **Reach:** a teacher may issue content or homework only for a section they hold a `Placement` on in the published timetable version. Head of Department reach extends across their `Department`'s offerings. There is no "all sections" issue path below Vice-Principal. |
| BR-LRN-003 | **Publication gate:** every artifact is Draft until published; Draft affects nothing and is invisible in the portal (BR-GLB-031), consistent with BR-SEC-012 — the portal shows finished work only. Publication is the event families see and the event that raises notifications. |
| BR-LRN-004 | **Homework:** issued to a section with a due date inside the academic year (BR-GLB-051) and on a working day per the school calendar (BR-GLB-052). Max marks are **optional** — an ungraded practice homework is legitimate and never reaches Module 17. A graded one must name the `BlueprintComponent` it will feed *before* it is published. |
| BR-LRN-005 | **Submission:** one live submission per student per homework; a resubmission supersedes and retains the prior as a version. Late submission is **accepted and flagged, never silently refused** — the lateness policy decides the mark penalty, not the acceptance. The submission stream is an append-only log: never `[Audited]` (auditing a log is circular). |
| BR-LRN-006 | **Student uploads** ride the existing attachment pipeline unchanged: typed per `DocumentType`, size-limited via `UploadLimitPolicy`, and virus-scanned through `IVirusScanner` **before a teacher can open one**. An unscanned or infected file is never served, to staff or to the portal. |
| BR-LRN-007 | **Question bank** per offering, versioned. A question referenced by any sitting is frozen: an edit creates a new version, so a past paper always renders as it was answered. Deprecating a question removes it from future picks and from nothing already sat (BR-GLB-006). |
| BR-LRN-008 | **Paper generation is blueprint-reconciled:** the generated item count and mark total must reconcile to the `BlueprintComponent` the paper will feed. Module 17 owns the weight; this module matches it or **refuses to publish the paper**. A mismatch is a blocking refusal, bilingual, naming both totals. |
| BR-LRN-009 | **Online sitting:** an open/close window (UTC), a duration, per-student resumability after a disconnect, and a deterministic shuffle seeded per student so a reload renders identically. **Time is server-authoritative** — a client clock never ends, extends or restores a sitting. |
| BR-LRN-010 | A sitting attached to a Module 16 `Exam` does **not** replace its logistics: that exam's schedule, invigilation roster and incident log still govern. This module supplies the paper and the capture surface; Module 16 remains the exam of record. |
| BR-LRN-011 | **Auto-marking covers objective item types only** — single choice, multiple choice, true/false, numeric with tolerance, exact-match short text. Constructed responses enter a manual marking queue. A sitting is not *marked* until every item carries a score; a partly-marked sitting can never be released. |
| BR-LRN-012 | **Mark handoff:** releasing a marked homework or sitting writes a **raw mark** into Module 17's marksheet against its `BlueprintComponent`, and then rides WF-07 unchanged. This module never computes a grade, never publishes a result, and never bypasses the approval chain. Re-releasing a corrected mark is a mark *change* and inherits Module 17's change control (T1 audit, mandatory reason). |
| BR-LRN-013 | **Portal write surface (new to the product):** portal writes are confined to the signed-in student's own submissions and sittings, are CSRF-protected and rate-limited, and widen no staff surface — BR-GLB-073 and BR-SEC-010 hold unchanged. **A parent account may view but never submit on a child's behalf**; the submitting identity is the student's own account. |
| BR-LRN-014 | **Academic integrity:** attempt telemetry (focus loss, paste, resume count, IP change) is recorded as a **flag for the teacher's judgement, never an automatic accusation and never an automatic mark penalty**. An integrity case escalates into Module 25's existing case flow; this module raises the flag and stops there. |
| BR-LRN-015 | **Audit:** marks and mark changes are T1 with a mandatory reason (BR-GLB-080); lesson, homework, bank and paper *definitions* are T2; submission, answer and autosave streams are append-only and are **excluded from audit by design** — a live sitting autosaves continuously and is exactly the high-churn shape that must never be `[Audited]`. |
| BR-LRN-016 | **No delete** (BR-GLB-005): a lesson is retired, a homework is withdrawn (with reason — and once any submission exists, only before the due date), a sitting is voided, a bank question is deprecated. Withdrawal notifies every student who had already submitted. |
| BR-LRN-017 | **Closed years** (BR-GLB-021/022): content and sittings in a closed year are read-only. Posting a late mark into one requires the closed-year permission and an audited reason, exactly as elsewhere. |
| BR-LRN-018 | **Accessibility and fairness:** per-student accommodations (extra time, a paused window) are configurable against the sitting and recorded on the attempt, so a moderator can see why one student had longer. Extra time is granted, never taken. |

## 4. Workflow

**Content** — draft → published → retired (P1, teacher-owned).
**Homework** — draft → issued → collecting → marking → released (P1). Release feeds Module 17's marksheet; WF-07 owns approval from there.
**Paper** — draft → blueprint reconciliation (BR-LRN-008) → approved (P2, Head of Department) → scheduled to a sitting.
**Sitting** — scheduled → open → in-progress (per student) → submitted → auto-marked → manual queue → released.
**Integrity flag** — raised → reviewed → dismissed, or escalated into Module 25's discipline case (no mark effect from this module).

## 5. User roles

Teacher (content, homework and marking for their own placements), Head of Department (bank governance, paper approval), Exam Officer (links a sitting to a Module 16 exam; owns the logistics), Vice-Principal / Registrar (windows, accommodations, integrity oversight), **Student** (portal: read content, submit homework, sit an exam), **Parent** (portal: read-only — what was set, what was submitted, what was scored once released).

## 6. Permissions

Module code `LRN` (free — verified against `ScreenCatalog.Modules`). Portal-facing screens take permissions in the **`POR` space, not `LRN`**, so a portal grant can never widen into a staff one — the existing rule for the portal's permission space holds.

| Screen | Verbs | Roles |
|--------|-------|-------|
| Lesson planner | View, Create, Edit, Deactivate | Teacher (own placements), HoD |
| Resource library | View, Create, Edit, Deactivate | Teacher, HoD |
| Homework desk | View, Create, Edit, Deactivate | Teacher (own placements) |
| Marking queue | View, Edit | Teacher, HoD |
| Question bank | View, Create, Edit, Deactivate | HoD (Teacher: Create/Edit own offerings) |
| Paper builder | View, Create, Edit, **Approve** | Teacher (build), HoD (approve) |
| Sitting console | View, Create, Edit, Deactivate | Exam Officer, VP |
| Integrity review | View, Edit | VP, HoD |
| Analytics | View, Export | Teacher (own), HoD, VP |
| Portal: my work / my sitting | View, **Submit** | Student (`POR` space, own records only) |

No new verb is required — `Submit` (5) carries student submission and `Approve` (6) carries paper approval, per the standard taxonomy.

## 7. Database concept

Schema `lrn`. **Name collisions were checked against `src/Sms.Domain` before proposing these** — `Exam` (Examinations), `Session` (Timetable) and `Enrollment` (Students) are all taken and are deliberately *not* reused. `Assignment` is avoided although free: `Teachers.Assignments` already means teacher-subject allocation in this product, and the collision would be semantic rather than compiler-visible.

Entities: `Lesson` (offering, optional `SessionId`, week, bilingual title/objectives, status) · `LessonResource` (+ `AttachmentId`, versioned) · `Homework` (offering, section, due date, optional `BlueprintComponentId`, max marks, lateness policy, status) · `HomeworkSubmission` (student, submitted-at, late flag, score, feedback) · `SubmissionVersion` (append-only) · `QuestionBank` (offering) · `Question` (type, bilingual stem, marks, version, deprecated flag) · `QuestionOption` (bilingual, correct flag) · `OnlinePaper` (bank, blueprint component, generation rule, totals, approval state) · `PaperItem` (frozen question version, order, marks) · `OnlineSitting` (paper, section(s), window, duration, optional Module 16 `ExamId`, status) · `SittingAttempt` (student, started/submitted, shuffle seed, resume count, accommodation, integrity flags) · `SittingAnswer` (item, response, auto score, manual score, marker) · `IntegrityFlag` (attempt, type, raised/reviewed state).

All are `ISchoolScoped`; everything below `Lesson`/`Homework`/`OnlineSitting` is also `IYearScoped` and carries its own `SchoolId` (the tenant filter must hold at every level, not only at the aggregate root). `Question` and `OnlinePaper` are **versioned catalogs and therefore do not take `ISoftActiveFiltered`** — a frozen past paper must stay loadable.

## 8. Required screens

1. **Lesson planner** — offering × week grid; drafts, publish, attach resources; optional bind to a dated session.
2. **Resource library** — per offering, versioned, typed, size- and scan-gated (doc 10 embed).
3. **Homework desk** — issue to section, due date + calendar validation, blueprint component picker (graded only), lateness policy, publish.
4. **Submission tracker** — per homework: submitted / late / missing roster with one-click chase (Module 33 notification).
5. **Marking queue** — mark and give feedback, bulk-release; refuses release while any item is unscored (BR-LRN-011).
6. **Question bank** — per offering, typed items, bilingual stems, versions, deprecation, usage count.
7. **Paper builder** — generation rule (by topic/difficulty/type), live blueprint reconciliation meter (BR-LRN-008), HoD approval.
8. **Sitting console** — window, duration, accommodations, cohort, optional link to a Module 16 exam; live monitor of who is in progress.
9. **Integrity review** — flagged attempts with their telemetry; dismiss or escalate to Module 25.
10. **Portal — my work** *(student)*: what is set, what is due, submit with upload; *(parent)*: the same, read-only.
11. **Portal — my sitting** *(student)*: the timed paper, autosave, resume, submit. **The first write screen in the portal.**
12. **Analytics** — per offering/section: submission rates, item difficulty and discrimination from real attempts, topic weakness.

## 9. Validation rules

Teacher must hold the placement (BR-LRN-002) · due date within year and on a working day · graded homework must name a blueprint component before publish · paper totals must reconcile to that component or publishing refuses, naming both numbers · a question in use cannot be edited in place, only re-versioned · sitting window inside the academic year and not overlapping the same cohort's other sitting · duration ≤ window length · release blocked while any answer is unscored · withdrawal after the due date blocked once submissions exist · portal submit accepted only for the signed-in student's own homework/attempt · every upload typed, within limit, and virus-clean before it is served. **Every one of these refusals ships Arabic and English at the Web boundary** — engine exception text is never surfaced raw.

## 10. Reports

Homework issued vs submitted per section/teacher · Missing-work register per student (feeds tutor conversations) · Sitting participation and completion · Item analysis (difficulty, discrimination, distractor pull) · Mark distribution per paper vs the term's other components · Integrity flag register 🔒 · Content coverage vs syllabus plan per offering · Teacher activity summary (issued, marked, turnaround time).

## 11. Dashboard widgets

Teacher: marking backlog, homework due today, last sitting's completion. HoD: papers awaiting approval, bank coverage per offering, turnaround outliers. VP: submission rate by grade, open integrity flags. Portal: due this week (student), and for parents, submitted/missing at a glance.

## 12. Notifications

`HomeworkPublished` → section's students + parents · `HomeworkDueSoon` (configurable lead) → students · `HomeworkOverdue` → student + parents · `HomeworkWithdrawn` → anyone who had submitted · `SubmissionReceived` → student (receipt) · `MarkReleased` → student + parents (only after Module 17 publication rules allow) · `SittingScheduled` / `SittingOpening` → students · `SittingSubmitted` → student receipt · `IntegrityFlagRaised` → HoD/VP · `PaperAwaitingApproval` → HoD. All bilingual and template-driven (BR-GLB-100), routed by the existing Module 33 engine.

## 13. Future enhancements

OMR bubble-sheet scanning for paper sittings (named in R3 alongside this module; deliberately **not** in this scope — it is a scanning-and-recognition problem, not an LMS one) · SCORM/xAPI package import · live virtual classrooms · adaptive practice sets from item analysis · proctoring integrations · peer assessment · rubric-scored constructed responses shared with Module 17's KG rubric work · offline-capable portal submission for low-connectivity homes.

## 14. Open questions

1. **Build or partner — still undecided.** The GAP register schedules that decision for end of R1 and it has not been taken. This document scopes the *build*; if the answer turns out to be partner (Classera / Google Classroom / Teams), §7 and §8 largely fall away and are replaced by an integration contract. **Confirm the decision before any code is written.**
2. **The portal becomes writable.** It has never accepted a write — `PortalController` contains no `[HttpPost]` at all, and the product's own user guide tells parents the portal "displays and does not edit". Online exams and homework submission end that. Confirm the owner accepts the widened attack surface, and that BR-SEC-012's "published data only" is understood to govern *reads* only.
3. **Student accounts at scale.** Portal accounts exist for demo purposes; an LMS requires every student to have a working, recoverable account. There is still **no reset-password screen in the product** — a forgotten password today is recovered by a developer calling a service. That is not viable for a school-wide LMS and is a prerequisite, not a detail.
4. **Do primary/KG grades submit online at all?** Proposed: content and homework visibility for all stages, submission and sittings gated per stage by school configuration. Confirm.
5. **Lateness policy** — school-wide default vs per-homework. Proposed: school default, per-homework override, penalty applied at marking rather than automatically.
6. **Retention.** Submissions and attachments are student work and accumulate fast. How many years are kept live before archive? Interacts with Module 35 and with storage sizing no other module has stressed.
7. **Connectivity and load for sittings.** Resumability is specified (BR-LRN-009), but a school sitting 200 students concurrently on unreliable Wi-Fi is a load profile this product has never been measured against. A performance budget (NF-P) needs setting before build.
