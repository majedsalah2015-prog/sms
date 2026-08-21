# Database 04 — Indexes, Constraints & Performance

**Phase:** 10 | **Status:** Draft for review | **Owner:** Database Architect + QA Architect

---

## 1. Indexing strategy (rules, then key prescriptions)

**Rules:**
1. Every FK gets a nonclustered index unless a covering composite already leads with it.
2. Tenant-first composites: hot-path indexes lead `(SchoolId, AcademicYearId, …)` matching the global query filters — the optimizer then serves every scoped query from one shape.
3. Filtered unique indexes implement business singletons (one Active year, one active enrollment per student-year, one primary teacher per offering-section).
4. Covering (`INCLUDE`) indexes only for measured hot reports — added at implementation with the workload, not speculatively; the prescriptions below are the analysis-known hot paths.
5. No index proliferation on write-hot audit/ledger tables beyond their read patterns.

**Key prescriptions (analysis-known hot paths):**

| Table | Index | Serves |
|-------|-------|--------|
| ppl.Enrollment | UQ filtered (StudentId, AcademicYearId) WHERE active; IX (SchoolId, AcademicYearId, GradeYearProfileId) | BR-GLB-024; rosters |
| ppl.SectionMembership | IX (SectionId, EffectiveTo) ; IX (EnrollmentId, EffectiveFrom) | membership-at-date resolution |
| acad.Session | UQ (PlacementId, SessionDate); IX (SchoolId, SessionDate) INCLUDE (Status, ActualTeacherId, ActualRoomId) | daily ops/cover console |
| acad.AttendanceDay | UQ (EnrollmentId, AttendanceDate); IX (SchoolId, AttendanceDate, Status) | daily absence report (RPT-ATD-001) |
| acad.AttendancePeriod | UQ (SessionId, EnrollmentId) | period capture |
| acad.MarkEntry | UQ (MarksheetId, EnrollmentId); IX (EnrollmentId) | sheets; student results |
| fin.Charge | UQ (SchoolId, ChargeNo); IX (PayerId, Status); IX (EnrollmentId, FeeCategoryId) | statements, positions |
| fin.Installment | IX (PayerId, DueDate, Status); IX (SchoolId, DueDate) WHERE unpaid | dunning, collection calendar |
| fin.PaymentAllocation | IX (InstallmentId); IX (ReceiptId) | position math both directions |
| fin.Receipt | UQ (SchoolId, ReceiptNo); IX (TillSessionId); IX (PayerId, PostedAtUtc) | continuity, day close |
| aud.AuditEntry | IX (EntityType, EntityId, OccurredAtUtc); IX (ActorUserId, OccurredAtUtc); IX (SchoolId, OccurredAtUtc) | explorer paths |
| msg.Delivery | IX (SchoolId, Status, QueuedAtUtc) WHERE pending/failed | ops queue |
| svc.WalletLedger | IX (WalletId, Id) | balance derivation |
| doc.Attachment | IX (OwnerEntityType, OwnerEntityId); IX (DocumentTypeId, ExpiryDate) WHERE tracked | entity docs; expiry console |

## 2. Constraint catalog (beyond FKs/UQs above)

| Kind | Examples |
|------|----------|
| CHECK | `CK_Charge_Amounts` (Gross=Net+Vat, all ≥ 0) · `CK_Room_ExamCap` (ExamCapacity ≤ Capacity) · `CK_ScaleBand_Range` (Min ≤ Max) · `CK_Contract_Dates` (From < To) · `CK_PDC_FutureDated` at lodgement (app-enforced; DB sanity Amount > 0) · `CK_Installment_Amount` (> 0) |
| Filtered UQ (singletons) | one Active AcademicYear/school · one Preparation/school · one active Enrollment/student/year · one primary TeacherAssignment/offering-section (WHERE Role=Primary AND EffectiveTo IS NULL) · one open TillSession/till · one Wallet/student |
| Trigger guards (defense-in-depth only — domain enforces first) | posted financial documents immutable (BR-GLB-062) · past Session immutability (status-flow columns exempt) · `aud.AuditEntry` deny UPDATE/DELETE (also via permissions) |
| App-principal permissions | app login: no DDL; no UPDATE/DELETE on `aud.*`; purge procedures under separate elevated principal (BR-SYS-005 execution path) |
| Cross-row rules (domain-enforced + integrity report) | ≥1 financially-responsible link (BR-PAR-005) · schedule Σ = charges (BR-INS-002) · allocation Σ = receipt (BR-PAY-003) — nightly reconciliation job flags violations (defense) |

## 3. Numbering implementation (doc 08)

Strict series: `core.NumberingSeries` + `SeriesState` row per series-scope; issuance via `usp_IssueNumber` taking an app lock per series (`sp_getapplock`) inside the posting transaction — gap-free under concurrency (BR-NUM-003), number materializes only on commit. Normal series: sequence objects acceptable. Series cutover per BR-NUM registry rules; continuity report (RPT-PAY-002) reads state vs issued rows.

## 4. Read models & reporting layer

| Read model (view / indexed view / snapshot table) | Feeds |
|---------------------------------------------------|-------|
| `vw_StudentPosition` (charges − credits − discounts − allocations per enrollment/payer) | statements, aging, dashboards (BR-FEE-008 single math) |
| `snap_AgedReceivables` (daily job snapshot) | RPT-FEE-004, Finance dashboards (C15/D refresh classes, Module 31 Q1) |
| `vw_AttendanceRates` (BR-ATD-009 canonical) | report cards, dashboards, RPT-ATD-* |
| `snap_DailyAttendanceSummary` | Principal/VP widgets |
| `vw_TeacherLoad` | RPT-TCH-001, load board |
| `vw_SeatUtilization` (capacity vs enrollment vs pipeline) | RPT-GRD-002, admissions widgets |
| `snap_CollectionCalendar` | RPT-INS-001, cashflow widgets |
| `vw_WalletBalance` | cafeteria (ledger-derived, BR-CAF-007) |
| `vw_EffectivePermissions` | security caching layer feed |
| Result broadsheet views per published batch | RPT-GRA-001 family |

Snapshots refresh via `ops.JobDefinition` schedules; every snapshot row carries `AsOfUtc` (BR-DSH-002 as-of display). Heavy statutory/analytical reports (RPT class An/Stat) read snapshots, never hot tables, satisfying NF-P5 without OLTP contention.

## 5. Volume estimates & partitioning (5,000-student school, 10-year horizon — NF-P1/P6)

| Stream | Est. rows/year | 10-yr | Strategy |
|--------|----------------|-------|----------|
| AttendancePeriod | ~7M (5k × 8 × 180) | 70M | BIGINT; partition by AcademicYearId; archive-year filegroups |
| AttendanceDay | ~0.9M | 9M | same scheme |
| Session | ~0.5M | 5M | partition by year |
| MarkEntry | ~1.5M | 15M | partition by year |
| aud.AuditEntry | 5–15M | ≤150M | **partition by month (OccurredAtUtc)**; checkpoint per partition; compressed (page) |
| msg.Delivery | 1–3M | 30M | partition by month; purge per BR-NTF Q1 retention |
| WalletLedger / sale lines | 1–2M | 20M | year partitions |

Row/page compression on cold partitions; prior-year partitions marked read-only filegroups after Closed+Archived (BR-AYR-006) — cheap backup differentials (Module 35 sizing input). Multi-tenant-many-schools deployments multiply linearly; the per-school math stays the design unit (README Q2 model).

## 6. Performance acceptance gates (implementation QA)

NF-P3 (P95 ≤ 2 s) verified against seeded 5,000-student demo tenant (BR doc 02 §9) for: cashier screen position load, attendance sheet save (NF-P4 ≤ 1 s), marksheet open/save, pipeline board, statement render. Query-store baselines captured per release; regression gate in CI perf suite. These gates are the QA Architect's Phase-12-onward checklist item.

## 7. Open questions

1. Indexed views vs snapshot jobs for `vw_StudentPosition` under concurrent cashier load — prototype decision at implementation (both designs documented; snapshot is the fallback). |
2. Audit monthly partitioning: confirm SQL Server edition assumptions (partitioning requires no special edition since 2016 SP1 — verify customer on-prem floor edition in packaging). |
3. Attachment storage growth (doc 10 §7 estimate) vs FILESTREAM/external blob — external blob storage already decided (T-7); confirm no FILESTREAM anywhere (proposed: confirm, keep DB lean). |
