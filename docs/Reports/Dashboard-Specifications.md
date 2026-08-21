# Dashboard Specifications

**Phase:** 9 | **Status:** Draft for review | Consolidates module §11 widget lists into per-persona default layouts (BR-DSH-003), for the seven mandated personas. Specialized-role dashboards (Nurse, Librarian, Storekeeper, Cafeteria, Transport, Discipline Officer, Coordinator, HR, Deputy, HoD, Sys Admin, Auditor) are specified in their owning module docs §11 and registered the same way.

**Widget kinds:** KPI = number tile · Trend = chart · Queue = actionable list w/ count · Alert = exception strip · Links = quick actions.
**Refresh classes (Module 31 Q1):** L = live · C15 = 15-min cache · D = daily. Every widget drills to the named screen/report (BR-DSH-002).

---

## 1. Principal — "Is the school healthy today, and what needs my decision?"

| # | Widget | Kind | Source | Refresh | Drill |
|---|--------|------|--------|---------|-------|
| 1 | Awaiting my approval | Queue | doc 05 inbox | L | My Approvals |
| 2 | Today's attendance % (by stage) | KPI | BR-ATD-009 | C15 | RPT-ATD-001 |
| 3 | Enrollment vs capacity | KPI+bar | M05 capacity board | D | RPT-GRD-002 |
| 4 | Receivables + aging donut | KPI | BR-FEE-008 | C15 | RPT-FEE-004 |
| 5 | Collections MTD vs target | Trend | M21 | C15 | RPT-PAY-009 |
| 6 | Revenue given away (discounts YTD) | KPI | M22 | D | RPT-DIS-002 |
| 7 | Pass-rate summary (last published term) | KPI | M17 | D | RPT-GRA-002 |
| 8 | Sensitive changes today | KPI | doc 07 | C15 | RPT-AUM-001 |
| 9 | Severe discipline cases pending | Queue 🔒 | M25 | L | Case board |
| 10 | Re-registration conversion (season) | Trend | M03 | D | RPT-AYR-004 |
| 11 | Staff attendance today / turnover trend | KPI | M12 | C15 | RPT-EMP-004/007 |
| 12 | Data protected (backup assurance) | KPI | M35 | D | Protection dashboard |

## 2. Vice Principal — "Operations: coverage, conduct, communication."

| # | Widget | Kind | Source | Refresh | Drill |
|---|--------|------|--------|---------|-------|
| 1 | Today's cover status (uncovered = red) | Queue | M15 cover console | L | Daily cover console |
| 2 | Uncaptured attendance sections | Alert | M14 monitor | L | Attendance monitor |
| 3 | Awaiting my approval | Queue | doc 05 | L | My Approvals |
| 4 | Incidents today / trend vs last term | KPI 🔒 | M25 | C15 | RPT-DCP-005 |
| 5 | Merit : violation ratio | KPI | M25 | D | RPT-DCP-007 |
| 6 | Substitution load fairness (top 5) | List | M15 | D | RPT-TTB-004 |
| 7 | SLA-breaching parent threads | Queue | M32 | L | Thread inbox |
| 8 | Chronic absentees count | KPI | M14 | D | RPT-ATD-003 |
| 9 | Trip approvals & upcoming trips | Queue | M29 | L | Trip console |
| 10 | Announcement approvals pending | Queue | M32 | L | Moderation queue |

## 3. Registrar — "Records complete, pipelines moving."

| # | Widget | Kind | Source | Refresh | Drill |
|---|--------|------|--------|---------|-------|
| 1 | Admissions pipeline by stage | Bar | M09 board | C15 | Pipeline board |
| 2 | Pending registrations (approved, unconverted) | Queue | M09 | L | Registration wizard |
| 3 | Data-quality counters (docs/photos/contacts) | KPI | M10 | D | RPT-STU-007 |
| 4 | Dedup queue depth | Queue | M11 | L | Dedup workbench |
| 5 | Withdrawals in progress (clearance states) | Queue | M10 WF-03 | L | Withdrawal wizard |
| 6 | Rollover step progress (season) | Progress | M03 cockpit | C15 | Rollover cockpit |
| 7 | Unassigned students post-rollover | Alert | M06 | L | Assignment board |
| 8 | Certificate requests pending | Queue | M18 | L | Issuance desk |
| 9 | Expiring IDs (students) | KPI | doc 10 | D | Expiring documents console |
| 10 | Publication readiness per grade (results season) | Progress | M17 | C15 | Publication console |
| 11 | Exemption approvals pending | Queue | M10 | L | Exemptions |

## 4. Finance (Finance Manager) — "Money in, money owed, money leaking."

| # | Widget | Kind | Source | Refresh | Drill |
|---|--------|------|--------|---------|-------|
| 1 | Today's collections by method | KPI | M21 | L | RPT-PAY-001 |
| 2 | Receivables + aging | KPI | BR-FEE-008 | C15 | RPT-FEE-004 |
| 3 | Expected vs collected (month) | Trend | M20/M21 | C15 | RPT-PAY-009 |
| 4 | Expected vs posted gap (leakage) | Alert | M19 | D | RPT-FEE-003 |
| 5 | Overdue top-10 payers | List | M20 | C15 | RPT-INS-002 |
| 6 | Broken promises today | Queue | M20 | L | Follow-up queue |
| 7 | PDCs due this week / bounce alerts | Queue | M21 | L | PDC registry |
| 8 | Pending: refunds, discounts, credit notes | Queue | doc 05 | L | My Approvals |
| 9 | Unreconciled days | Alert | M21 | D | Reconciliation workbench |
| 10 | Late-fee proposals pending | Queue | M19 | D | Late-fee console |
| 11 | Wallet liability (cafeteria) | KPI | M27 | D | RPT-CAF-002 |
| 12 | Discount renewals queue (season) | Queue | M22 | D | Renewal queue |

## 5. Teacher — "My day, my classes, my duties." (workspace = dashboard, Module 13 §8.6)

| # | Widget | Kind | Source | Refresh | Drill |
|---|--------|------|--------|---------|-------|
| 1 | Today's sessions (rooms, cover flags) | Timeline | M15 | L | Session view (attendance entry) |
| 2 | Attendance to capture (deadline countdown) | Queue | M14 | L | Capture sheet |
| 3 | Marksheets due / returned | Queue | M17 | L | Marksheet workspace |
| 4 | My section today (homeroom): absentees, alerts | List | M14/M10 | L | Section roster |
| 5 | Unread parent threads | Queue | M32 | L | Thread inbox |
| 6 | My invigilation & substitution duties | List | M16/M15 | L | Duty sheets |
| 7 | My week at a glance | Calendar | M15/M04 | D | Personal timetable |
| 8 | Medical/custody alert badges (my students) | Alert 🔒-banner | BR-HLT-002 | L | (banner only) |
| 9 | My load summary | KPI | M13 | D | Load board |

## 6. Parent (portal home) — "My children, my money, my messages."

| # | Widget | Kind | Source | Refresh | Drill |
|---|--------|------|--------|---------|-------|
| 1 | Children cards (photo, section, today's status) | Cards | M10/M14 | L | Child profile |
| 2 | Total due + next installment | KPI | M19/M20 | C15 | My statement |
| 3 | Today: timetable & bus (per child) | Timeline | M15/M23 | L | Child schedule |
| 4 | Unread messages & letters needing ack | Queue | M32 | L | Messages |
| 5 | Latest published results | Card | M17 | On publish | Results |
| 6 | Attendance this month (per child) | Mini-calendar | M14 | C15 | Attendance calendar |
| 7 | Upcoming: events, exams, trips, consents pending | List | M04/M16/M29 | D | Family calendar |
| 8 | Cafeteria balance + low-balance alert | KPI | M27 | C15 | Wallet |
| 9 | Actions needed (excuses, consents, re-registration) | Queue | multi | L | Respective flows |

## 7. Student (portal home, config-gated stage) — "My day and my work."

| # | Widget | Kind | Source | Refresh | Drill |
|---|--------|------|--------|---------|-------|
| 1 | Today's timetable (rooms, changes) | Timeline | M15 | L | My schedule |
| 2 | Published results | Card | M17 | On publish | My results |
| 3 | Exam schedule & countdown | List | M16 | D | Exam schedule |
| 4 | My attendance summary | KPI | M14 | C15 | My attendance |
| 5 | Library: loans due | List | M26 | C15 | My loans |
| 6 | My activities this week | List | M29 | D | Activities |
| 7 | My achievements | Cards | M10/M29 | D | Achievements |
| 8 | School events strip | List | M04 | D | Calendar |

---

## Layout & governance rules

1. Row-1 placement is reserved for **action widgets** (queues/alerts) on staff dashboards — attention before information.
2. Every widget above must exist in the Module 31 registry with permission mapping before Phase 11 UI design; portal widgets obey BR-DSH-006 (published/committed data only) and BR-SEC-011 custody filtering.
3. Seasonal widgets (rollover, re-registration, renewals, results season) auto-appear during their windows and collapse otherwise (config).
4. Small-count masking (BR-DSH-007) applies to every aggregate 🔒-adjacent widget.
5. Specialized-role dashboards ship from module §11 lists using the same registry; no persona may exceed 12 default widgets (cognitive budget; users may personalize beyond).
