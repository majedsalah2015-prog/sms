# Database 02 — ER Model

**Phase:** 10 | **Status:** Draft for review | **Owner:** Database Architect

> Conceptual/logical ER diagrams per cluster, drawn from the approved module Database Concept sections. Cardinality: `||` one, `o{` many. Standard columns (§4 of doc 01) omitted from diagrams for legibility.

---

## 1. Core patterns (apply everywhere)

1. **Tenancy spine:** `core.School` ← every tenant table via `SchoolId` (ADR-2).
2. **Year spine:** `core.AcademicYear` ← every transactional table via `AcademicYearId` (ADR-3).
3. **Person vs participation:** identity tables (`Student`, `Parent`, `Employee`) are year-independent; participation tables (`Enrollment`, `TeacherAssignment`, `TransportSubscription`) are year-scoped (doc 02 §5).
4. **Effective-dating:** memberships and assignments use `EffectiveFrom/EffectiveTo` rows, never overwritten (BR-SCN-005, BR-TCH-002, BR-SCH-004).
5. **Official documents:** strict-numbered, immutable-after-post, corrected by reversal documents (BR-GLB-041/062).
6. **Snapshotting:** published results, certificates, rendered notifications, and workflow versions store their input snapshots (BR-GRA-003, BR-CRT-004, BR-NOT-008, BR-WF-008).

## 2. Core structure (schema `core`)

```mermaid
erDiagram
    School ||--o{ AcademicYear : has
    School ||--o{ Stage : offers
    AcademicYear ||--o{ Semester : contains
    Semester ||--o{ Term : contains
    AcademicYear ||--o{ CalendarDay : defines
    AcademicYear ||--o{ CalendarEvent : has
    Stage ||--o{ GradeLevel : contains
    GradeLevel ||--o{ GradeYearProfile : "versioned per year"
    AcademicYear ||--o{ GradeYearProfile : scopes
    GradeYearProfile ||--o{ Section : has
    GradeYearProfile ||--o{ CurriculumOffering : plans
    Subject ||--o{ CurriculumOffering : "offered as"
    Department ||--o{ Subject : groups
    Building ||--o{ Room : contains
    Room ||--o{ Section : "home room (0..1)"
    School ||--o{ LookupValue : "school-tier lists"
```

Notes: `GradeYearProfile` is the year-versioning vehicle (BR-GRD-008); `CurriculumOffering` — not `Subject` — is the FK target for placements, marksheets, assignments (BR-SUB model). `CalendarDay` materializes day types per date (BR-CAL §7).

## 3. People (schema `ppl`)

```mermaid
erDiagram
    Student ||--o{ Enrollment : "per year"
    AcademicYear ||--o{ Enrollment : scopes
    GradeYearProfile ||--o{ Enrollment : places
    Enrollment ||--o{ SectionMembership : "effective-dated"
    Section ||--o{ SectionMembership : holds
    Parent ||--o{ ParentStudentLink : "guardianship"
    Student ||--o{ ParentStudentLink : "linked to"
    ParentStudentLink ||--o| CustodyRestriction : "may carry"
    Student ||--o{ EmergencyContact : has
    Student ||--o{ SubjectExemption : "per offering"
    CurriculumOffering ||--o{ SubjectExemption : exempted
    AdmissionCampaign ||--o{ Application : receives
    Application }o--|| Parent : "dedup-linked"
    Application ||--o| Student : "converts to"
    Employee ||--o| TeacherProfile : "if teaching"
    Employee ||--o{ Contract : "effective-dated"
    Employee ||--o{ LeaveRequest : submits
    TeacherProfile ||--o{ TeacherAssignment : holds
    CurriculumOffering ||--o{ TeacherAssignment : taught
    Section ||--o{ TeacherAssignment : "in section"
    Section ||--o{ HomeroomAssignment : "effective-dated"
    TeacherProfile ||--o{ HomeroomAssignment : leads
```

Notes: `ParentStudentLink` serves both Module 10 and 11 views (one table). Withdrawal/offboarding cases, waiting lists, merges are workflow/state tables around these spines.

## 4. Academic operations (schema `acad`)

```mermaid
erDiagram
    TimetableVersion ||--o{ Placement : contains
    Placement }o--|| Section : for
    Placement }o--|| CurriculumOffering : teaches
    Placement }o--|| TeacherProfile : by
    Placement }o--|| Room : in
    Placement ||--o{ Session : "dated instances"
    Session ||--o| Substitution : "may have"
    Session ||--o{ AttendancePeriod : "period mode"
    Enrollment ||--o{ AttendanceDay : "daily mode"
    AttendanceDay ||--o| Justification : "may carry"
    ExamRound ||--o{ Exam : schedules
    Exam }o--|| CurriculumOffering : assesses
    Exam ||--o{ ExamSitting : "rooms"
    ExamSitting ||--o{ ExamAttendance : records
    Blueprint ||--o{ BlueprintComponent : weighs
    CurriculumOffering ||--o{ Blueprint : "per term"
    BlueprintComponent ||--o{ Marksheet : "entered via"
    Marksheet }o--|| Section : for
    Marksheet ||--o{ MarkEntry : holds
    Enrollment ||--o{ MarkEntry : "per student"
    GradingScale ||--o{ ScaleBand : maps
    Enrollment ||--o{ TermResult : computed
    TermResult }o--|| GradingScale : "banded by (snapshot)"
    Enrollment ||--o{ YearResult : aggregated
    YearResult ||--o| PromotionProposal : drives
    CertificateType ||--o{ CertificateIssue : issues
    Student ||--o{ CertificateIssue : "for (or Employee)"
```

Notes: one marks store (`Marksheet`/`MarkEntry`) fed by Modules 16+17 (BR-EXM-007 note); results tables persist calculation snapshots (JSON column) per BR-GRA-003.

## 5. Finance (schema `fin`)

```mermaid
erDiagram
    Payer ||--o| Parent : "backed by (v1)"
    FeeCategory ||--o{ FeeStructureLine : priced
    GradeYearProfile ||--o{ FeeStructureLine : "per grade-year"
    Enrollment ||--o{ Charge : owes
    Payer ||--o{ Charge : "billed to"
    FeeCategory ||--o{ Charge : classifies
    Charge ||--o{ CreditNote : "corrected by"
    PlanTemplate ||--o{ TemplateInstallment : splits
    Enrollment ||--o{ PlanAssignment : "per category group"
    PlanAssignment ||--o{ Installment : schedules
    Installment }o--o{ Charge : "scheduled-allocation lines"
    TillSession ||--o{ Receipt : issues
    Payer ||--o{ Receipt : pays
    Receipt ||--o{ PaymentAllocation : allocates
    Installment ||--o{ PaymentAllocation : "settled by"
    Payer ||--o{ PDC : lodges
    PDC ||--o| Receipt : "on clearance"
    Payer ||--o{ RefundVoucher : refunded
    DiscountType ||--o{ DiscountGrant : grants
    Enrollment ||--o{ DiscountGrant : benefits
    ScholarshipProgram ||--o{ DiscountGrant : "as scholarship"
```

Notes: `Payer` is the v1.x sponsor-ready abstraction (BR-FEE-004). Position/aging are views over Charge/CreditNote/DiscountGrant-applications/PaymentAllocation (BR-FEE-008) — no stored balance except materialized read models (doc 04 §4).

## 6. Services (schema `svc`) — spine view

```mermaid
erDiagram
    Route ||--o{ RouteStop : ordered
    Bus ||--o{ Route : serves
    Enrollment ||--o{ TransportSubscription : rides
    RouteStop ||--o{ TransportSubscription : "AM/PM stops"
    Route ||--o{ Trip : "dated runs"
    Trip ||--o{ TripLog : "board/alight"
    Student ||--|| MedicalFile : has
    MedicalFile ||--o{ ClinicVisit : records
    MedicalFile ||--o{ MedicationAuthorization : authorizes
    MedicationAuthorization ||--o{ AdministrationLog : logged
    Student ||--o{ Incident : "discipline"
    Incident ||--o| Case : "severity >= 2"
    Case ||--o{ ActionApplied : results
    Title ||--o{ Copy : holds
    Copy ||--o{ Loan : circulates
    Student ||--|| Wallet : "cafeteria"
    Wallet ||--o{ WalletLedger : "derived balance"
    StoreItem ||--o{ Variant : "sizes/colors"
    Bundle ||--o{ BundleLine : contains
    Program ||--o{ ProgramEnrollment : enrolls
    ProgramEnrollment }o--|| ConsentRecord : requires
```

## 7. Platform (schemas `sec`, `aud`, `msg`, `doc`, `ops`) — spine view

```mermaid
erDiagram
    UserAccount }o--|| Person : "student/parent/employee"
    UserAccount ||--o{ RoleAssignment : holds
    Role ||--o{ RolePermission : grants
    RoleAssignment ||--o{ ScopeGrant : "school/year/grade/section"
    WorkflowDefinition ||--o{ WorkflowInstance : runs
    WorkflowInstance ||--o{ WorkflowStep : trails
    AuditEntry }o--|| UserAccount : by
    IntegrityCheckpoint ||--o{ AuditEntry : covers
    Template ||--o{ TemplateVersion : versioned
    NotificationEvent ||--o{ Delivery : "per recipient-channel"
    Attachment }o--|| DocumentType : typed
    Attachment ||--o{ AttachmentVersion : versions
    ReportDefinition ||--o{ ReportExecution : logs
    ImportBatch ||--o{ ImportRow : contains
```

Notes: `Attachment` links to owners polymorphically (`OwnerEntityType` + `OwnerEntityId` + enforced registry of valid owner types — reviewed pattern, accepted for the document store only; everywhere else FKs are physical). `AuditEntry` uses the same logical polymorphism by necessity (doc 07 §4 business-key capture compensates).

## 8. Cross-cluster dependency map

`core` ← everyone. `ppl` ← acad/fin/svc. `fin.Payer` ← ppl.Parent. `acad.Session` ← core placement chain. `sec/aud/msg/doc/ops` ← consumed product-wide via services (not FK-heavy — loose coupling by id + registry where polymorphic).

Estimated table count: **~190 tables** (inventory in doc 03) + ~35 views/read models (doc 04).
