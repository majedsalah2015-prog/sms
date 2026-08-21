# Module 23 — Transportation

**Phase:** 7 — Student services | **Status:** Draft for review | **Rule prefix:** `BR-TRN`

---

## 1. Purpose

Manage school transport end-to-end: buses, drivers/attendants, routes and stops, student assignments, trip-level bus attendance (safety-critical), and transport fee triggers — with the "no child left on a bus / at a stop" control as the design center.

## 2. Scope

**In:** fleet registry (buses, documents ⏰), transport staff (drivers/attendants — employee or contractor), route & stop design (AM/PM, geo), student transport registration & stop assignment, capacity management, trip execution (boarding/alighting logs), safety events (not-boarded, end-of-trip sweep), transport fee generation (Module 19 trigger, zone-based pricing), suspension for arrears (policy-gated per BR-INS-008), ad-hoc trips (activities).
**Out:** GPS hardware tracking (Future, interface-noted), fee collection (Module 21), driver payroll (Module 12 for employees; contractor invoices out of product).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-TRN-001 | **Fleet:** buses registered with plate, capacity, type; mandatory expiry-tracked documents (registration, insurance, safety inspection — BR-ATT-008); an expired-document bus is flagged Unroadworthy and blocked from trip assignment (override: Principal T1, emergency only). |
| BR-TRN-002 | **Transport staff:** drivers (license ⏰ mandatory, class-validated) and attendants; contractors carry the same document rules via a light contractor record; staff assigned per route/shift; substitutions logged (mirror BR-TTB-007 pattern, simplified). |
| BR-TRN-003 | **Routes & stops:** route = ordered stops with times, direction (AM pickup / PM drop), assigned bus + driver + attendant; stop = named point (bilingual) with optional geo; a route's student count ≤ bus capacity (hard; waiting list per route mirrors BR-ADM-006 pattern). |
| BR-TRN-004 | **Student registration:** transport is a service subscription per year (start/end dates): student × AM stop × PM stop (may differ); registration triggers the transport charge (BR-FEE-003 service-linked, zone/route-based pricing per structure, pro-rated per BR-FEE-006); ending the subscription (family choice/withdrawal WF-03) ends charges per policy. |
| BR-TRN-005 | **Trip execution:** each route-day-direction = a trip; attendant logs boarding/alighting per student (roster from subscriptions); **safety rules:** student not boarded AM → immediate parent notification (urgent class, bypasses quiet hours per BR-NOT-004); end-of-trip sweep confirmation ("bus empty") mandatory to close a trip; PM student not collected at school → escalation to supervisor. Unclosed trips escalate (BR-ATD-007 pattern). |
| BR-TRN-006 | Only pickup-authorized persons (BR-PAR-008) may receive a student at a PM stop where school policy requires handover confirmation (stage-configurable — KG strict, secondary self-release flag per parent consent). |
| BR-TRN-007 | Route changes (stop moved, time shift) notify affected families (BR-NOT); mid-year stop/route reassignment of a student re-prices per zone policy (delta charge/credit note per BR-FEE rules, permission-gated). |
| BR-TRN-008 | Arrears suspension (where policy allows, BR-INS-008): suspension list is Principal-approved, effective-dated, notified formally; safety exception: never mid-trip. |
| BR-TRN-009 | Trip logs are T2-audited; safety events T1; subscriptions/charges follow finance audit rules. |

## 4. Workflow

Subscription: request (portal/counter) → capacity check (waitlist if full) → stop assignment → charge posted (P2 Finance visibility) → active. Trip: open → board/alight logs → sweep → close (P1 logged). Safety events: auto-escalation cases. Route redesign (season): draft → publish (P2 Transport Supervisor + VP) with family notifications.

## 5. User roles

Transport Supervisor (owner), Attendants (trip logging — mobile-first screen), Drivers (view manifests), Registrar (subscriptions), Finance (charges), Parents (portal: stop info, notifications, requests), Principal (suspensions, overrides).

## 6. Permissions

| Action | Roles |
|--------|-------|
| Fleet & staff registry | Transport Supervisor |
| Route/stop design & publish | Supervisor (+VP P2) |
| Subscriptions & stop assignment | Supervisor, Registrar |
| Trip logging | Attendant (own trip) |
| Safety-event handling | Supervisor + escalation chain |
| Suspension list | Principal approval |
| Reports | Supervisor, Principal, Finance (fee views) |

## 7. Database concept

Entities: `Bus` (+ documents), `TransportStaff` (employee ref or contractor), `Route` (+ `RouteStop` ordered, times), `TransportSubscription` (student, AM/PM stops, dates, zone price ref), `Trip` (route × date × direction, status), `TripLog` (student boarding/alighting events, actor, time), `SafetyEvent` (type, escalation state), `RouteWaitlist`. Zone pricing lives in fee structures (transport category variants per zone). |

## 8. Required screens

1. Fleet & staff registry with document expiry console (doc 10 embed).
2. **Route designer** — ordered stop list with times, map view (geo optional v1: static coordinates), student roster per stop, capacity meter.
3. Subscription desk — student search, stop pick (AM/PM), price preview, waitlist handling.
4. **Trip console (attendant, mobile-first)** — roster with photos, tap board/alight, not-boarded flag, sweep confirmation, offline-tolerant sync (connectivity reality on buses; single-batch upload). |
5. Supervisor live board — today's trips status, safety events, unclosed trips.
6. Portal: child's route/stop/times, absence-from-bus notice (parent declares "not riding today" — suppresses not-boarded alert), change requests.

## 9. Validation rules

Capacity hard-checks; driver license class vs bus type; document-expired blocks (BR-TRN-001); subscription dates within year; stop times sequential along route; trip close requires all students resolved (alighted/absent-declared/escalated) + sweep; PM handover confirmation per stage policy; suspension effective dates ≥ notice period per policy. |

## 10. Reports

Route manifests (per trip, printable for attendant fallback) · Ridership & utilization per route (seats vs subscribed vs actual) · Not-boarded / safety event register · Document expiry (fleet & drivers) · Transport revenue vs cost centers (fee feed) · Stop-level demand map (route planning) · Trip punctuality (logged times vs plan) · Suspension register 🔒. |

## 11. Dashboard widgets

Supervisor: live trip statuses, open safety events, expiring documents. Principal: transport utilization %, safety events this term. Portal: bus today (trip started/arriving-window static v1). |

## 12. Notifications

`StudentNotBoarded` (AM) → parents **urgent**; `StudentBoarded/Alighted` (config, often KG-only) → parents; `RouteChanged` → affected families; `TripDelayed` (manual trigger BR-NOT catalog) → route families; `SweepMissing` → Supervisor urgent; `SubscriptionActivated/Suspended` → parents + Finance; `BusDocumentExpiring` → Supervisor. |

## 13. Future enhancements

GPS/telematics integration (live bus map, geofenced stop ETAs, auto trip logs); RFID/QR student tap-on-tap-off; route optimization engine; driver behavior monitoring; contractor invoice reconciliation.

## 14. Open questions

1. Are attendants universal in target markets (KSA requires supervisors on school buses) — confirm per pack; if driver-only buses exist, trip logging falls to driver UX. |
2. Zone-based vs flat transport pricing prevalence — structure supports both; confirm typical for defaults. |
3. Parent "not riding today" declaration cutoff time (proposed: 1h before trip) — confirm. |
4. Boarding notifications default (all stages vs KG-only) — proposed KG-only to limit noise; confirm. |
