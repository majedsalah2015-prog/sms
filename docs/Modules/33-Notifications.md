# Module 33 — Notifications (Administration)

**Phase:** 8 — Platform | **Status:** Draft for review | **Rule prefix:** `BR-NTF`

> The notification **framework** (model, event catalog, rules BR-NOT-###) is approved doc [09](../09-Notifications.md). This module is its **administration and operations surface**; it adds operational rules only.

---

## 1. Purpose

Give schools full operational control of the doc 09 engine: subscription configuration, bilingual template management, provider/channel operations, delivery monitoring, cost control, and the per-user notification center.

## 2. Scope

**In:** event–subscription configuration UI, template editor lifecycle (draft/test/publish), provider management (email/SMS/WhatsApp credentials, sender IDs, failover order), delivery-log operations (retry, investigate), cost counters & budgets, quiet-hours/digest settings, user notification center & preferences, undeliverable-contact data-quality loop (BR-NOT-006 → Registrar queue).
**Out:** the engine model itself (doc 09), human-composed messaging (Module 32).

## 3. Business rules (operational additions to BR-NOT-###)

| ID | Rule |
|----|------|
| BR-NTF-001 | Template publishing is versioned with mandatory test-send before first publish; published versions are immutable (sent-content snapshots per BR-NOT-008 reference the version). |
| BR-NTF-002 | Subscription changes to statutory/safety event classes (BR-NOT-007) require P2 (Principal) — a school cannot silently disable absence or safety notifications below the product floor. |
| BR-NTF-003 | Provider credentials are stored encrypted, entered by Sys Admin, verifiable via test action; provider failover order configurable; provider outage auto-alerts (JobFailed class). |
| BR-NTF-004 | SMS/WhatsApp budgets per BR-NOT open question resolved: **alert at 80%, optional hard-stop per school; safety-class messages exempt from hard-stop** (never block a not-boarded alert on budget). |
| BR-NTF-005 | Delivery-failure handling: auto-retries per BR-NOT-006; post-retry failures for mandatory classes surface in an operations queue; bounced contacts flow to the Registrar data-quality queue with the affected student/parent context. |
| BR-NTF-006 | Configuration changes T1-audited (they alter what parents legally receive); delivery logs retained ≥ 2 years (dispute evidence: "we notified you on…"). |

## 4. Workflow

Template lifecycle (draft→test→publish, P1 with publish permission). Statutory subscription changes (P2). Provider changes (Sys Admin, T1). Operations queue handling (P1 logged).

## 5. User roles

Sys Admin (providers, engine ops), Communications/VP (templates, subscriptions), Principal (statutory approvals), Registrar (data-quality queue), Finance (budget counters), all users (own notification center/preferences), Parents (portal preferences per BR-NOT-007 opt-out classes).

## 6. Permissions

Provider config (Sys Admin) · Template publish (Comms + permission) · Subscription config (Comms; statutory + P2) · Operations queue (Sys Admin) · Budget config (FM + Sys Admin) · Own preferences (everyone).

## 7. Database concept

Administers doc 09 entities (`Template` versions, `SubscriptionRule`, `Provider`, `DeliveryLog`, `BudgetCounter`, `OpsQueueItem`, `UserPreference`). No new core entities beyond versioning/ops wrappers.

## 8. Required screens

1. Event–subscription matrix (doc 09 §5) with product-floor indicators (what can't be disabled).
2. Template studio — bilingual side-by-side editor, placeholder picker with validation, preview per channel, test-send, version history.
3. Provider console — credentials, sender IDs, failover, health/test.
4. Delivery operations — log explorer, failure queue, retry actions, bounce→data-quality dispatch.
5. Budget console — counters per channel/school, thresholds.
6. Notification center (all users) + preference screens (staff & portal variants).

## 9. Validation rules

Placeholders must resolve against the event payload (publish-blocked otherwise, BR-NOT model); test-send before first publish; statutory floor enforced in the matrix UI (disabled toggles with explanation); budget hard-stop cannot include safety classes (BR-NTF-004); provider deletion blocked while referenced by active rules.

## 10. Reports

(Per doc 09 §6) delivery success by channel/provider · spend & volume per school/month · undeliverable-contact report · volume by event type · template change register (T1 view).

## 11. Dashboard widgets

Sys Admin: failed deliveries today, provider health, queue depth. FM: channel spend vs budget. Registrar: data-quality queue depth.

## 12. Notifications (meta)

`ProviderDown` → Sys Admin urgent; `BudgetThreshold` → FM; `MandatoryDeliveryFailing` → Sys Admin + Comms; `TemplatePublished` → Comms trail.

## 13. Future enhancements

Per doc 09 §8 (push, per-event parent channel choice, WhatsApp interactive) plus: A/B wording tests for dunning effectiveness (with Module 20 metrics); notification analytics (open/click where channel supports).

## 14. Open questions

1. Delivery-log retention 2 years (proposed) vs pack-driven — align with BR-AUD-006 country values. |
2. Should parents choose channel per event class (doc 09 Future) get pulled into v1 for absence notifications specifically? Proposed: no (config complexity) — revisit at pilot feedback. |
