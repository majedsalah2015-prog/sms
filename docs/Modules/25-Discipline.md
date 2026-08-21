# Module 25 — Discipline

**Phase:** 7 — Student services | **Status:** Draft for review | **Rule prefix:** `BR-DCP`

---

## 1. Purpose

A fair, documented behavior system: positive recognition (merits) alongside violations, a configurable code of conduct with severity levels and graduated consequences, case workflows with evidence and appeals, and behavior analytics — restricted-category data handled with due process.

## 2. Scope

**In:** behavior code catalog (violations by severity + merits), incident recording (WF-11), point systems (config), case management (investigation → decision → action → appeal), consequence catalog (warning → detention → suspension ladder per policy & pack legality), behavior contracts/pledges, parent meetings log, merit/reward programs, report-card behavior feed (Module 17 config), exam-incident intake (BR-EXM-007).
**Out:** counseling (Module 24 Q3 — out of v1), staff discipline (HR domain, out of product v1), criminal matters (out — escalation note only).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-DCP-001 | **Behavior code** per school (country-pack starter — e.g., KSA behavior regulation levels): violation types with severity level (1–4 typical), default consequence ladder per level & repetition count, merit types with points; bilingual; year-versioned; published to families (portal handbook). |
| BR-DCP-002 | **Recording:** any teacher/supervisor records incidents (own-scope students or witnessed); severity 1 (minor) may resolve teacher-level (recorded, parent-notified per config); severity ≥ 2 opens a **case** (WF-11) routed to the discipline officer/supervisor. Merits record freely (P1) with points. |
| BR-DCP-003 | **Case workflow (WF-11):** `Reported → Under Investigation (statements, evidence via doc 10 🔒) → Decision (P3: Officer → VP; severity 4 → + Principal / committee P5) → Action applied → (Appeal window) → Closed`; student/parent statement step mandatory for severity ≥ 3 (due process); decisions reference the code article (no free-form punishments). |
| BR-DCP-004 | **Consequences:** from the catalog only (verbal/written warning, parent summons, detention, community service, activity/trip ban, in-school suspension, external suspension days per pack legality, behavior contract); suspension-class actions check country-pack legal limits (max days, ministry notification requirements) and always require Principal; **corporal punishment is not representable** (product stance). |
| BR-DCP-005 | Repetition escalation is computed (same/similar violation count within period → next ladder step proposed); proposals are advisory — deciders may deviate downward with reason (never above-code without Principal). |
| BR-DCP-006 | **Appeals:** parent appeal within N days (config) for severity ≥ 2 → review by next-level authority (not the original decider, BR-WF-003 spirit) → uphold/modify/dismiss; one appeal per case. |
| BR-DCP-007 | **Points & aggregation:** violation/merit points aggregate per term; thresholds trigger flags (welfare review, honor list); report-card behavior grade/comment derives per school config (points band or qualitative) — Module 17 consumes. |
| BR-DCP-008 | **Visibility:** discipline data is restricted 🔒 (BR-GLB-072): discipline roles, Principal, homeroom (own students); portal shows parents their child's incidents/decisions per school policy level (full / decisions-only / summons-only); records never appear in transcripts or certificates unless the pack's conduct-certificate rules require a conduct grade (Module 18 conduct certificate uses the derived grade only). |
| BR-DCP-009 | Behavior contracts and keep-apart pairs feed Sections balancing (BR-SCN-008) under the same restricted visibility. |
| BR-DCP-010 | Cases/decisions T1-audited 🔒; recording-user identity protected from portal display (teacher-protection stance — parents see the school, not the reporter, per config default). |

## 4. Workflow

WF-11 per BR-DCP-003 with severity routing; exam incidents auto-open cases at configured severity (BR-EXM-007); suspension actions trigger attendance integration (suspension days = recorded status per pack rules) and ministry notification tasks where required.

## 5. User roles

Discipline Officer / Student Affairs Supervisor (owner), VP (decisions), Principal (severe cases, appeals), Teachers (recording, merits), Homeroom (own-section visibility, parent meetings), Committee members (P5 severe cases), Parents (portal per policy, appeals), Auditor (register).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Record incidents/merits | Teachers (scope), Supervisors |
| Manage cases | Officer; decisions per severity chain |
| Approve suspensions | Principal (always) |
| Handle appeals | Next-level authority |
| View records 🔒 | Discipline roles, Principal, Homeroom (own) |
| Configure code | Officer + Principal (P2) |
| Portal visibility | Parent (own child, policy level) |

## 7. Database concept

Entities: `BehaviorCode` (+ `ViolationType`/`MeritType`, ladders, versions); `Incident` (numbered doc 08, reporter, severity, narrative, evidence refs 🔒); `Case` (workflow state, statements, decision + code article ref); `ActionApplied` (consequence, dates, completion); `Appeal`; `PointLedger` (per student-term); `BehaviorContract`; `ParentMeeting`. Attendance/ministry side-effects via events. |

## 8. Required screens

1. Code designer — violation/merit catalog with ladder visualization, version publish.
2. Quick-record (teacher) — student picker (roster), type, narrative, evidence photo; merit variant one-tap.
3. **Case board** — officer kanban by state, SLA aging, severity filters.
4. Case file — timeline (statements, evidence 🔒, decisions, actions, appeal), print for committee.
5. Action tracker — detentions roster, suspension calendar, contract reviews due.
6. Analytics — heatmaps (violation types × grade/section/time-of-day), repeat-offender list 🔒, merit leaderboards (positive-first display).
7. Portal: child behavior view per policy, appeal submission, handbook.

## 9. Validation rules

Incident requires type + narrative (+ evidence rules per severity); case decisions must cite code article; suspension length ≤ pack max; appeal within window, once; statements mandatory severity ≥ 3; deviation-below-proposal requires reason; merit points within type bounds; contract requires signatures (e-ack portal or scanned pledge doc 10). |

## 10. Reports

Incident register per period/severity 🔒 · Case cycle-time & outcomes (due-process health) · Consequence register (esp. suspensions, ministry format per pack) · Appeals outcomes · Behavior trends (type × time × location patterns) · Repeat-offender welfare list 🔒 · Merit/points summary per section (positive culture metric) · Conduct-grade derivation register (Module 17/18 feed) · Parent-meeting log. |

## 11. Dashboard widgets

Officer: open cases by state/SLA, today's detention roster. VP/Principal: severe cases pending, incidents trend vs last term, merit:violation ratio. Homeroom: my section's flags (welfare-first framing). Portal: child summary per policy.

## 12. Notifications

`IncidentRecorded` (severity-gated per BR-NOT catalog) → parents; `SummonsIssued` → parents (formal, numbered letter Module 18 pattern); `DecisionIssued` → parents (+ appeal window info); `ActionScheduled` (detention/suspension dates) → parents + affected teachers; `AppealDecided` → parents; `ContractReviewDue` → officer, homeroom; `ThresholdFlag` (welfare) → officer, homeroom 🔒. |

## 13. Future enhancements

Restorative-practice workflows (mediation tracks); welfare early-warning composite (with Modules 14/17 signals — governance-reviewed); positive-behavior rewards store (points redemption with Module 28); anonymous bullying report channel (safeguarding review needed); CCTV evidence linkage governance.

## 14. Open questions

1. Country-pack behavior regulations (KSA لائحة السلوك والمواظبة levels/points; UAE equivalents) — pack content needed for starter codes and suspension limits. |
2. Portal visibility default (full vs decisions-only) — proposed decisions-only; confirm school culture fit. |
3. Reporter anonymity from parents (BR-DCP-010) default on — confirm legal stance per pack. |
4. Do conduct grades appear on report cards in all target curricula (drives Module 17 config defaults)? |
