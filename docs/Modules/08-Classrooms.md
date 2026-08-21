# Module 08 — Classrooms

**Phase:** 3 — Academic structure | **Status:** Draft for review | **Rule prefix:** `BR-ROM`

---

## 1. Purpose

Manage the school's physical spaces — classrooms, labs, gyms, halls — with capacity, type, equipment, and availability, so Sections get home rooms, Timetable gets conflict-free room allocation, and Examinations get seating capacity.

## 2. Scope

**In:** building/floor structure, room catalog (type, capacity, exam capacity, gender-wing tag, equipment), availability & maintenance status, section home-room linkage (with Module 06), room booking for events (light), utilization reporting.
**Out:** timetable session placement (Module 15 — it consumes rooms + availability), asset management/depreciation (out of product scope), exam seating charts (Module 16 — consumes exam capacity).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-ROM-001 | Rooms are organized as Building → Floor → Room; room code unique per school; bilingual names; type from lookup (classroom, science lab, computer lab, gym, hall, library room, clinic, prayer room…). |
| BR-ROM-002 | Two capacities per room: **standard capacity** (teaching) and **exam capacity** (spaced seating, ≤ standard); Sections validate against standard (BR-SCN-002), Examinations against exam capacity. |
| BR-ROM-003 | Rooms may carry a **gender-wing tag** (boys/girls/shared) for segregated campuses (BR-GRD-004 reality); section/timetable placement validates section gender vs wing. |
| BR-ROM-004 | Room **availability**: default = school working hours; per-room exceptions (half-day availability, out-of-service ranges). A room under maintenance (status + date range) is excluded from placement; existing timetable sessions on it surface as conflicts to resolve (Module 15 consumes the event). |
| BR-ROM-005 | Equipment/features are tagged from a lookup (projector, smartboard, lab benches, AC…); subject offerings may declare required features (e.g., Chemistry → science lab) — Timetable placement treats requirements per configured strictness (hard/warn), mirroring BR-SUB-006 pattern. |
| BR-ROM-006 | A section's home room link (BR-SCN-002) is exclusive per timetable model choice: in the default "students stay, teachers move" model, one room maps to at most one section as home; in "subject rooms" mode (secondary option) home links are optional. School chooses the model per stage (feeds Module 15 config). |
| BR-ROM-007 | Rooms with historical usage are deactivatable only (BR-GLB-005); deactivation with future sessions triggers the conflict flow (BR-ROM-004). |
| BR-ROM-008 | Changes are T3-audited; maintenance status changes T2 (they disrupt operations). |

## 4. Workflow

Direct-entry module (no approval chains): catalog and availability edits audited; maintenance status set by Sys Admin/Facilities with date range; conflict resolution happens in Timetable (Module 15) driven by events from here. Event bookings (light): request → approve (P2, VP) when booking displaces teaching sessions; free-slot bookings direct.

## 5. User roles

Sys Admin / Facilities Coordinator (author), Registrar (section links), Timetable owner (consumer), VP (booking approvals), Exams officer (exam capacity consumer).

## 6. Permissions

| Action | Roles |
|--------|-------|
| View rooms/availability | All academic staff |
| Edit catalog/equipment | Sys Admin, Facilities |
| Set maintenance status | Sys Admin, Facilities |
| Link home rooms | Registrar |
| Book rooms (events) | Staff request → VP approval when displacing |

## 7. Database concept

Entities: `Building` / `Floor` (light hierarchy); `Room` (code, bilingual, type, standard capacity, exam capacity, wing tag, status); `RoomFeature` (room × feature lookup); `RoomAvailabilityException` (room, range, reason: maintenance/reserved); `RoomBooking` (event use). Timetable sessions and section home links reference Room; conflict detection is Module 15 logic over this module's availability data.

## 8. Required screens

1. Room catalog — tree (building/floor) + grid, capacity columns, wing/type filters, feature tags.
2. Room detail — features, availability calendar (sessions from timetable overlaid read-only), maintenance history.
3. Maintenance console — set out-of-service ranges, see impacted sessions count (link to Module 15 resolution).
4. Booking calendar — event bookings vs teaching load, request/approve flow.
5. Utilization heatmap — rooms × periods occupancy (consumes timetable).

## 9. Validation rules

Unique codes; exam capacity ≤ standard capacity; wing tag mandatory when the school flags itself gender-segregated; maintenance range valid + reason mandatory; home-room exclusivity per BR-ROM-006 model; deactivation/maintenance with future sessions requires acknowledging the conflict list (resolution itself in Module 15).

## 10. Reports

Room inventory sheet (bilingual, by building) · Utilization report (occupancy % per room per week — underused-space signal) · Maintenance log · Exam-capacity summary (total seats by wing — feeds exam planning) · Feature coverage (labs per stage vs curriculum needs).

## 11. Dashboard widgets

Facilities/Sys Admin: rooms under maintenance now, upcoming out-of-service. Timetable owner: unresolved room conflicts count. VP: pending booking approvals.

## 12. Notifications

`RoomMaintenanceSet` → Timetable owner, affected teachers (via session impact); `BookingRequested/Decided` → requester, VP; `MaintenanceEnding` (D-2) → Facilities.

## 13. Future enhancements

QR room signage linking to live schedule; IoT occupancy sensors; asset register integration; room-change same-day announcements to student portal (with Module 15 substitutions).

## 14. Open questions

1. Timetable model per stage (BR-ROM-006: home-room vs subject-room) — confirm both models are truly needed in v1 target market (assumed: home-room dominant in K-9, subject-rooms for labs only). Decision shapes Module 15 significantly.
2. Are event bookings (§4) in v1 scope or Future? Recommendation: **keep the light version** (it protects timetable integrity); confirm appetite.
3. Exam capacity per room: single value or per exam-type layouts? v1: single value; layouts to Future with seating charts (Module 16 Q).
