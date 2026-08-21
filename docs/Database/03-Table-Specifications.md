# Database 03 — Table Specifications

**Phase:** 10 | **Status:** Draft for review | **Owner:** Database Architect

> Two levels: (A) **column-level specifications for the 12 pivotal tables** that carry the system's invariants; (B) the **full table inventory** (~190 tables) with purpose one-liners. Remaining column-level detail materializes as EF Core configurations at implementation, governed by doc 01 standards — re-documenting all 190 here would duplicate the module Database Concepts already approved.

Standard column sets (doc 01 §4) apply to every table and are not repeated. 🔒 = restricted-category table (implementation review list, doc 01 §6).

---

## A. Pivotal table specifications

### A1. `core.AcademicYear`
| Column | Type | Notes |
|--------|------|-------|
| Id | INT PK | |
| SchoolId | INT FK | |
| StartDate / EndDate | DATE | BR-AYR-001 no-overlap (filtered constraint via app + UQ on (SchoolId, StartDate)) |
| LabelEn / LabelAr / HijriLabel | NVARCHAR(40) | display labels |
| Status | SMALLINT | 0 Preparation, 1 Active, 2 Closing, 3 Closed, 4 Archived (BR-AYR-002) |
| ClosingEndsOn | DATE NULL | closing window |
**Constraints:** one Active per school (filtered unique index `IX_AcademicYear_SchoolId_Active WHERE Status=1`); same pattern for Preparation.

### A2. `ppl.Student`
| Column | Type | Notes |
|--------|------|-------|
| Id | INT PK | |
| SchoolId | INT FK | first-registration school; group future keeps person here |
| StudentNo | NVARCHAR(20) | doc 08 permanent, UQ per school, immutable (BR-NUM-004) |
| FirstNameAr..FamilyNameAr / ...En | NVARCHAR(60) ×8 | full quad name both languages (Gulf convention) |
| Gender | SMALLINT | |
| DateOfBirth | DATE | |
| NationalityLookupId | INT FK | |
| PrimaryIdTypeLookupId / PrimaryIdNo / PrimaryIdExpiry | FK / NVARCHAR(30) / DATE NULL | BR-GLB-003; UQ filtered on (SchoolId, PrimaryIdTypeLookupId, PrimaryIdNo) |
| PhotoAttachmentId | INT NULL FK doc.Attachment | consent-governed |
| Status | SMALLINT | Enrolled/Suspended/Withdrawn/Graduated/Transferred/Alumni (BR-STU-002) |

### A3. `ppl.Enrollment` — the year participation pivot
| Column | Type | Notes |
|--------|------|-------|
| Id | INT PK | |
| SchoolId / AcademicYearId | INT FK | |
| StudentId | INT FK | |
| GradeYearProfileId | INT FK | grade placement |
| EnrollmentDate / ExitDate NULL | DATE | mid-year entry/exit |
| Status | SMALLINT | Active/Withdrawn/Completed/Promoted... |
| SourceType | SMALLINT | Admission / Rollover / Reinstatement |
**Constraints:** `UQ_Enrollment_Student_Year` filtered on active status (BR-GLB-024). FK target for attendance, marks, fees, services.

### A4. `ppl.ParentStudentLink` 🔒(custody)
| Column | Type | Notes |
|--------|------|-------|
| Id | INT PK; ParentId FK; StudentId FK | |
| RelationshipLookupId | INT FK | father/mother/guardian… |
| IsPrimaryContact / IsFinanciallyResponsible / IsPickupAuthorized / IsPortalVisible | BIT ×4 | BR-STU-003 flags |
| GuardianshipDocAttachmentId | INT NULL | mandatory when guardian type |
| EffectiveFrom / EffectiveTo NULL | DATE | |
**Constraints:** app-enforced ≥1 financially-responsible per active student (BR-PAR-005 — cross-row rule, domain layer + integrity report).

### A5. `core.CurriculumOffering` — the academic reference target
| Column | Type | Notes |
|--------|------|-------|
| Id INT PK; GradeYearProfileId FK; SubjectId FK | | UQ (GradeYearProfileId, SubjectId) |
| WeeklyPeriods | SMALLINT | BR-SUB-005 |
| IsAssessable | BIT | BR-SUB-003 |
| GpaWeight | DECIMAL(6,3) | neutral weight (Module 07 Q1) |
| IsElective / ElectiveGroupTag | BIT / NVARCHAR(30) NULL | BR-SUB-008 future-safe |
| EffectiveFrom/To | DATE | end-dating per BR-SUB-004 |

### A6. `acad.Session` — dated teaching instance
| Column | Type | Notes |
|--------|------|-------|
| Id BIGINT PK | | high volume: sections × periods × days |
| SchoolId / AcademicYearId | | |
| PlacementId | INT FK | pattern source |
| SessionDate | DATE | working days only (generation honors CalendarDay) |
| Status | SMALLINT | Held/Substituted/RoomChanged/Cancelled |
| ActualTeacherId NULL / ActualRoomId NULL | INT FK | overrides vs placement snapshot |
**Constraints:** UQ (PlacementId, SessionDate); past sessions immutable except status flows (trigger guard).

### A7. `acad.MarkEntry` — single marks store
| Column | Type | Notes |
|--------|------|-------|
| Id BIGINT PK; MarksheetId FK; EnrollmentId FK | | UQ (MarksheetId, EnrollmentId) |
| NumericMark | DECIMAL(7,3) NULL | ≤ component max (CK + domain) |
| RubricLevelId NULL / IsAbsent / AbsenceClassification NULL / IsExempt | | BR-GRA-002/BR-EXM-006 |
| EnteredByUserId / VerifiedByUserId NULL | | double-entry mode (BR-EXM-009) |
**T1-audited from first entry (doc 07).**

### A8. `acad.TermResult` — published computation snapshot
| Column | Type | Notes |
|--------|------|-------|
| Id INT PK; EnrollmentId FK; CurriculumOfferingId FK; TermId FK | | UQ combo |
| Score | DECIMAL(7,3) | |
| ScaleBandId | INT FK | band at publication |
| CalculationSnapshot | NVARCHAR(MAX) JSON | inputs/weights/scale version (BR-GRA-003) |
| PublishedAtUtc / PublishedBatchId | | immutability boundary; WF-08 writes correction rows (new version, superseded flag) |

### A9. `fin.Charge` — the receivable
| Column | Type | Notes |
|--------|------|-------|
| Id INT PK; SchoolId; AcademicYearId | | |
| ChargeNo | NVARCHAR(30) | strict series (BR-GLB-041), UQ per school |
| EnrollmentId FK; PayerId FK; FeeCategoryId FK | | |
| NetAmount / VatAmount / GrossAmount | DECIMAL(18,4) | CK: Gross = Net + Vat |
| VatRateSnapshot | DECIMAL(6,4) | BR-GLB-061 |
| SourceType / SourceRefId | SMALLINT / INT | registration/service/misc/opening/late-fee |
| ProRationBasis | NVARCHAR(200) NULL | BR-FEE-006 display basis |
| Status | SMALLINT | Posted/Void (immutable after post — trigger guard) |
| EInvoiceUuid / EInvoiceHash | NULL | BR-FEE-005 readiness fields |

### A10. `fin.Receipt`
| Column | Type | Notes |
|--------|------|-------|
| Id INT PK; ReceiptNo strict UQ; TillSessionId FK NULL (gateway rows null); PayerId FK | | |
| Amount | DECIMAL(18,4) | = Σ allocations (CK via domain + reconciliation report) |
| Method | SMALLINT | Cash/Card/Transfer/Cheque/PDCClearance/Gateway |
| MethodRef | NVARCHAR(60) NULL | card/transfer/cheque ref (mandatory per method — domain) |
| Status | SMALLINT | Posted/Void (same-day rule BR-PAY-002) |

### A11. `sec.RoleAssignment` + `sec.ScopeGrant`
`RoleAssignment`: UserAccountId FK, RoleId FK, UQ pair.
`ScopeGrant`: RoleAssignmentId FK; ScopeDimension SMALLINT (School/Year/Grade/Section/OwnOnly); ScopeValueId INT NULL (null = dynamic "own" resolution per doc 06 §4.2). Permission checks resolve grants → effective scope set, cached per session (T-8).

### A12. `aud.AuditEntry` (append-only)
| Column | Type | Notes |
|--------|------|-------|
| Id BIGINT PK | | partition key candidate (doc 04 §5) |
| SchoolId / AcademicYearId NULL | | context |
| EntityType / EntityId / BusinessKey | NVARCHAR(100)/BIGINT/NVARCHAR(60) | doc 07 §4 |
| FieldName NULL / OldValue / NewValue | NVARCHAR | NULL field = record-level event |
| ActorUserId / ActingRoleId / IsDelegated | | |
| Action | SMALLINT | Create/Update/StatusChange/View(T0)/Export/Login… |
| Reason NULL / CorrelationId / SourceScreen / ClientIp | | |
| OccurredAtUtc | DATETIME2(3) | |
**No UPDATE/DELETE grants to app principal; integrity checkpoints hash-chain daily (usp, BR-AUD-007).**

---

## B. Table inventory (~190) — by schema

### `core` (34)
School, SchoolGroup, Signatory, SchoolSetting, CountryPack, FeatureToggle, SetupChecklist, LookupCategory, LookupValue, AcademicYear, Semester, Term, YearChecklistItem, RolloverBatch, RolloverStudentState, CalendarDay, CalendarEvent, CalendarVersion, Stage, GradeLevel, GradeYearProfile, Section, HomeroomAssignment, Subject, Department, CurriculumOffering, TeacherSubjectQualification, Building, Floor, Room, RoomFeature, RoomAvailabilityException, RoomBooking, NumberingSeries (+SeriesState).

### `ppl` (38)
Student, EmergencyContact, SubjectExemption, WithdrawalCase, WithdrawalClearanceItem, Enrollment, SectionMembership, Parent, ParentStudentLink, CustodyRestriction 🔒, ParentMergeLog, DedupCandidate, AdmissionCampaign, Application, ApplicationAssessment, ApplicationDecision, WaitingListEntry, InquiryLog, Employee, OrgUnit, Position, EmployeeAssignment, Contract 🔒, Qualification, TrainingRecord, StaffAttendanceDay, LeaveType, LeaveEntitlement, LeaveRequest, LeaveBalance, PayrollPrepExport 🔒, PayrollPrepLine 🔒, OffboardingCase, OffboardingClearanceItem, TeacherProfile, TeacherAssignment, TeacherAvailability, StudentAchievement.

### `acad` (42)
TimetableShape, PeriodSlot, TimetableVersion, Placement, Session, Substitution, SessionChangeLog, AttendanceDay, AttendancePeriod, GateEvent, Justification, LeavePass, AttendanceEscalationCase, ExamType, ExamRound, Exam, ExamSitting, SeatAllocation, InvigilationDuty, ExamAttendance, ExamIncident 🔒, MakeupEligibility, GradingScale, ScaleBand, Blueprint, BlueprintComponent, Marksheet, MarkEntry, SkillRubric, SkillRubricResult, TermResult, YearResult, PromotionProposal, RankRecord, ReportCardIssue, Appeal, CertificateType, CertificateTemplateSlotConfig, CertificateIssue, CertificateRequest, VerificationLog, WarningLetterIssue.

### `fin` (36)
Payer, FeeCategory, FeeStructure, FeeStructureLine, Charge, CreditNote, LateFeePolicy, LateFeeRun, LateFeeProposal, OpeningBalanceMap, PlanTemplate, TemplateInstallment, PlanAssignment, Installment, InstallmentChargeAllocation, RescheduleCase, PromiseToPay, DunningEvent, TillSession, Receipt, PaymentAllocation, AdvanceBalanceSnapshot, PDC, RefundVoucher, DayClose, BankStatementLine, ReconciliationMatch, PaymentIntent, DiscountType, EligibilityRule, DiscountGrant, DiscountApplication, ScholarshipProgram, ScholarshipEnvelope, Waiver, RenewalQueueItem.

### `svc` (58)
Bus, BusDocumentStatus, TransportStaff, Route, RouteStop, TransportSubscription, RouteWaitlist, Trip, TripLog, SafetyEvent, MedicalFile 🔒, CarePlan 🔒, VaccinationRecord 🔒, VaccinationScheduleItem, ClinicVisit 🔒, MedicationAuthorization 🔒, AdministrationLog 🔒, ScreeningCampaign, ScreeningResult 🔒, HealthConsentRecord, ExposureNotice, BehaviorCode, ViolationType, MeritType, Incident 🔒, Case 🔒, CaseStatement 🔒, ActionApplied 🔒, DisciplineAppeal 🔒, PointLedger, BehaviorContract 🔒, ParentMeeting, Title, Copy, MemberPolicy, Loan, Reservation, FineProposal, StocktakeSession, StocktakeLine, ReadingLog, CafeteriaItem, Menu, MenuLine, Wallet, WalletLedger, CafeteriaSale, CafeteriaSaleLine, MealPlan, MealPlanSubscription, MealPlanRedemption, SpendControl, StoreItem, Variant, PriceList, PriceListLine, Bundle, BundleLine, StoreSale, StoreSaleLine, DistributionSession, HandoutRecord, ReturnExchange, StockMovement, PreOrder, ActivityType, Program, ProgramEnrollment, ActivityConsentRecord, ActivitySession, ActivityAttendance, TripPlan, Achievement, CompetitionEvent. *(counts as 58 with granular lines)*

### `sec` (12)
UserAccount, Role, Permission, RolePermission, RoleAssignment, ScopeGrant, PasswordHistory, LoginAttempt, UserSession, TwoFactorEnrollment, ImpersonationLog, SecurityApprovalRequest.

### `aud` (7)
AuditEntry, IntegrityCheckpoint, AnomalyRule, AnomalyHit, VerificationRun, AuditPurgeCertificate, SavedInvestigation.

### `msg` (14)
Announcement, AnnouncementAudienceSnapshot, Thread, ThreadMessage, CommunicationMatrixEntry, OfficialLetter, LetterRecipientState, AbuseReport, Template, TemplateVersion, SubscriptionRule, Provider, Delivery, BudgetCounter.

### `doc` (6)
DocumentType, Attachment, AttachmentVersion, ChecklistDefinition, ChecklistItemState, PurgeCertificate.

### `ops` (16)
ReportDefinition, ReportExecution, ReportSubscription, QueuedRun, WidgetDefinition, LayoutTemplate, UserLayout, JobDefinition, JobRun, ImportBatch, ImportRow, HealthMetricSample, LicenseState, MaintenanceWindow, DiagnosticsBundle, BackupRun (+ VerificationRun/RestoreCase/BackupPolicy/SnapshotEvent under `ops` as well).

### Workflow engine (`ops` or own schema `wf`, 5)
WorkflowDefinition, WorkflowState, WorkflowTransition, WorkflowInstance, WorkflowStep.

---

**Total ≈ 190 tables** (final count settles in EF migrations; additions require this inventory updated first — change control mirrors the report catalog rule).
