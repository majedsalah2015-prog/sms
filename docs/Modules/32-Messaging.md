# Module 32 — Messaging

**Phase:** 8 — Platform | **Status:** Draft for review | **Rule prefix:** `BR-MSG`

> Boundary (doc 09 note): Notifications = system-generated events. **Messaging = human-composed communication**: announcements, threads, and official letters. Both deliver through the doc 09 channel infrastructure.

---

## 1. Purpose

Controlled school↔family and internal communication: broadcast announcements with audience builders, two-way parent↔staff threads (policy-gated), internal staff messaging, and numbered official letters — replacing the WhatsApp chaos with accountable, bilingual, archived channels.

## 2. Scope

**In:** announcements (audience-targeted broadcasts, approval-gated), conversation threads (parent↔homeroom/teacher/registrar per policy matrix; internal staff threads), official letters (numbered via doc 08 Message Ref series, template-based — summons, warnings, circulars), audience builder (by school/stage/grade/section/route/activity/custom lists), attachments (doc 10 rules), moderation & retention, translation aids (compose bilingual).
**Out:** channel transport (doc 09 engine), automated event notifications (doc 09), real-time chat presence (explicitly not a chat app — asynchronous model), video conferencing (out).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-MSG-001 | **Announcements:** composed bilingual (or auto-flagged single-language), audience via builder (resolved recipient list snapshotted at send), channel selection per doc 09 config; school-wide or cross-grade sends require approval (P2: VP/Principal per school config); section-level by homeroom within scope needs none (speed). All sends logged with reach counts (BR-GLB-102). |
| BR-MSG-002 | **Threads:** parent-initiated threads route per the school's **communication matrix** (topic → role: absence → homeroom; fees → finance; complaint → management), not to personal inboxes by name; staff replies within SLA targets (doc 05-style visibility); threads are school records — retained, searchable by management per policy, never deletable by participants (BR-GLB-032 spirit). |
| BR-MSG-003 | **Teacher↔parent boundaries:** teachers message only parents of their scoped students (doc 06 dynamic scopes); no parent↔parent messaging (product stance); student messaging (upper stages, config) limited to academic threads with teacher, parent-visible flag per school policy. |
| BR-MSG-004 | **Official letters** use templates + the Message Ref series (doc 08), render as documents (school identity/signatory per BR-SCH), deliver via portal + configured channels, and register per recipient with read/acknowledgment tracking (summons ack, circular read receipts); acknowledgment-required letters escalate unacknowledged after N days. |
| BR-MSG-005 | Working-hours etiquette: staff-side sending respects configured quiet hours for non-urgent classes (BR-NOT-004 alignment); urgent override permission-gated (Principal-delegable). |
| BR-MSG-006 | Content rules: attachments per doc 10 (no executables), size limits; profanity/abuse reporting flow (recipient reports → VP review); moderation queue optional per school for teacher→parent broadcasts. |
| BR-MSG-007 | Retention: threads and announcements retained ≥ academic year + configured horizon; exports permission-gated (T0); personal-data minimization in broadcasts (no marks/fees details in announcements — deep-link to portal instead, BR-NOT-010 alignment). |

## 4. Workflow

Announcement approval (P2 above scope thresholds). Thread routing per matrix (P1). Official letters: compose → chain per letter type (summons = discipline/attendance flows trigger them; circulars = Principal) → send → ack tracking. Abuse reports: VP review (P2).

## 5. User roles

All staff (scoped messaging), Communications Officer / VP (announcements, moderation), Principal (approvals, urgent override), Parents (threads per matrix, receive), Students (config-limited), Auditor (logs).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Section announcements | Homeroom (own) |
| Grade/stage/school announcements | Supervisor/VP → approval per threshold |
| Thread participation | Per communication matrix + scopes |
| Official letters | Per letter-type chain |
| Search/oversight of threads | Management per policy (logged T0) |
| Urgent override | Principal (delegable) |

## 7. Database concept

Entities: `Announcement` (+ audience snapshot, channel results per doc 09 log), `Thread` + `ThreadMessage` (topic type, routing, participants), `CommunicationMatrix` (topic → role config), `OfficialLetter` (numbered, template, per-recipient register + ack state), `AbuseReport`. Delivery via doc 09 infrastructure (`per-recipient delivery log reuse`).

## 8. Required screens

1. **Compose announcement** — bilingual editor side-by-side, audience builder with live count, channel picker + cost estimate (SMS counters, BR-NOT-006), schedule send, approval submission.
2. Inbox/threads — unified staff inbox (my threads by topic/SLA), parent portal messages view.
3. Communication matrix config — topic→role routing per school.
4. Official letter center — templates, generate (single/batch), ack tracking board.
5. Moderation & abuse queue (VP).
6. Archive search (management, permission-gated).

## 9. Validation rules

Audience must resolve > 0; bilingual completeness warning (single-language send is explicit choice); approval thresholds by audience size/scope; letters require template mandatory fields; ack-required letters must define escalation days; attachment rules per doc 10; quiet-hours enforcement with urgent-permission check.

## 10. Reports

Announcement reach & read rates · Thread volume & SLA by topic/role (responsiveness metric) · Official letter register (with ack status — feeds discipline/attendance compliance) · SMS/WhatsApp spend by sender (with BR-NOT-006 counters) · Abuse report outcomes · Unacknowledged summons list.

## 11. Dashboard widgets

VP: pending approvals, SLA-breaching threads, abuse queue. Homeroom/Teacher: my unread threads. Parent portal: unread messages, letters requiring acknowledgment.

## 12. Notifications

Delivery itself rides doc 09. Meta-events: `AnnouncementApprovalPending` → approver; `ThreadReplyReceived` → participant; `LetterUnacknowledged` (escalation) → sender role; `AbuseReported` → VP.

## 13. Future enhancements

WhatsApp two-way threads (Business API interactive); auto-translation assist (compose once, AI-assist the second language with human confirm); PTA/committee group spaces; broadcast scheduling calendar with conflict warnings (avoid same-day message floods).

## 14. Open questions

1. Communication matrix starter defaults (topic list + routing) — propose product defaults; confirm with pilot school culture. |
2. Management thread-search policy default: enabled-with-T0-logging (proposed) vs off-until-enabled — staff-trust sensitivity; confirm. |
3. Read receipts for ordinary threads (not letters): show to staff? Proposed yes internally, not to parents. |
4. SMS cost visibility to senders (estimate at compose, proposed) — confirm schools want sender-level budget accountability. |
