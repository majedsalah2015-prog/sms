using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Admissions;
using Sms.Domain.Attachments;
using Sms.Domain.Attendance;
using Sms.Domain.Cafeteria;
using Sms.Domain.Calendar;
using Sms.Domain.Certificates;
using Sms.Domain.Classrooms;
using Sms.Domain.Discipline;
using Sms.Domain.Discounts;
using Sms.Domain.Employees;
using Sms.Domain.Examinations;
using Sms.Domain.Fees;
using Sms.Domain.GlExport;
using Sms.Domain.Grades;
using Sms.Domain.Grading;
using Sms.Domain.Health;
using Sms.Domain.Installments;
using Sms.Domain.Jobs;
using Sms.Domain.Library;
using Sms.Domain.Lookups;
using Sms.Domain.Notifications;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Transport;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Domain.Subjects;
using Sms.Domain.Teachers;
using Sms.Domain.Timetable;
using Sms.Domain.Workflow;
using AdmissionApplication = Sms.Domain.Admissions.Application;

namespace Sms.Infrastructure.Persistence
{
    /// <summary>
    /// The product's concrete context. Module entity sets accumulate here;
    /// mapping details live in configuration classes (docs/Database/01 §5).
    /// </summary>
    public class AppDbContext : SmsDbContext
    {
        public AppDbContext(DbContextOptions options, ITenantContext tenant, ICurrentUser currentUser, IClock clock, IAuditContext? auditContext = null)
            : base(options, tenant, currentUser, clock, auditContext)
        {
        }

        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

        public DbSet<Role> Roles => Set<Role>();

        public DbSet<Permission> Permissions => Set<Permission>();

        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

        public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

        public DbSet<ScopeGrant> ScopeGrants => Set<ScopeGrant>();

        public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

        public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

        public DbSet<UserSession> UserSessions => Set<UserSession>();

        public DbSet<TwoFactorEnrollment> TwoFactorEnrollments => Set<TwoFactorEnrollment>();

        public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();

        public DbSet<WorkflowState> WorkflowStates => Set<WorkflowState>();

        public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();

        public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

        public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

        public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();

        public DbSet<SeriesState> SeriesStates => Set<SeriesState>();

        public DbSet<Template> Templates => Set<Template>();

        public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

        public DbSet<SubscriptionRule> SubscriptionRules => Set<SubscriptionRule>();

        public DbSet<Provider> Providers => Set<Provider>();

        public DbSet<Delivery> Deliveries => Set<Delivery>();

        public DbSet<BudgetCounter> BudgetCounters => Set<BudgetCounter>();

        public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();

        public DbSet<Attachment> Attachments => Set<Attachment>();

        public DbSet<AttachmentVersion> AttachmentVersions => Set<AttachmentVersion>();

        public DbSet<LookupCategory> LookupCategories => Set<LookupCategory>();

        public DbSet<LookupValue> LookupValues => Set<LookupValue>();

        public DbSet<JobDefinition> JobDefinitions => Set<JobDefinition>();

        public DbSet<JobRun> JobRuns => Set<JobRun>();

        public DbSet<SchoolGroup> SchoolGroups => Set<SchoolGroup>();

        public DbSet<School> Schools => Set<School>();

        public DbSet<Signatory> Signatories => Set<Signatory>();

        public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

        public DbSet<Semester> Semesters => Set<Semester>();

        public DbSet<Term> Terms => Set<Term>();

        public DbSet<CalendarDay> CalendarDays => Set<CalendarDay>();

        public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

        public DbSet<CalendarVersion> CalendarVersions => Set<CalendarVersion>();

        public DbSet<Stage> Stages => Set<Stage>();

        public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();

        public DbSet<GradeYearProfile> GradeYearProfiles => Set<GradeYearProfile>();

        public DbSet<Section> Sections => Set<Section>();

        public DbSet<HomeroomAssignment> HomeroomAssignments => Set<HomeroomAssignment>();

        public DbSet<SectionMembership> SectionMemberships => Set<SectionMembership>();

        public DbSet<Department> Departments => Set<Department>();

        public DbSet<Subject> Subjects => Set<Subject>();

        public DbSet<CurriculumOffering> CurriculumOfferings => Set<CurriculumOffering>();

        public DbSet<TeacherSubjectQualification> TeacherSubjectQualifications => Set<TeacherSubjectQualification>();

        public DbSet<Student> Students => Set<Student>();

        public DbSet<Enrollment> Enrollments => Set<Enrollment>();

        public DbSet<StudentGuardianLink> StudentGuardianLinks => Set<StudentGuardianLink>();

        public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();

        public DbSet<Parent> Parents => Set<Parent>();

        public DbSet<Building> Buildings => Set<Building>();

        public DbSet<Floor> Floors => Set<Floor>();

        public DbSet<Room> Rooms => Set<Room>();

        public DbSet<RoomFeature> RoomFeatures => Set<RoomFeature>();

        public DbSet<RoomAvailabilityException> RoomAvailabilityExceptions => Set<RoomAvailabilityException>();

        public DbSet<RoomBooking> RoomBookings => Set<RoomBooking>();

        public DbSet<AdmissionCampaign> AdmissionCampaigns => Set<AdmissionCampaign>();

        public DbSet<AdmissionApplication> Applications => Set<AdmissionApplication>();

        public DbSet<ApplicationAssessment> ApplicationAssessments => Set<ApplicationAssessment>();

        public DbSet<WaitingListEntry> WaitingListEntries => Set<WaitingListEntry>();

        public DbSet<Employee> Employees => Set<Employee>();

        public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();

        public DbSet<EmployeeAssignment> EmployeeAssignments => Set<EmployeeAssignment>();

        public DbSet<Contract> Contracts => Set<Contract>();

        public DbSet<Qualification> Qualifications => Set<Qualification>();

        public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();

        public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();

        public DbSet<AttendanceDay> AttendanceDays => Set<AttendanceDay>();

        public DbSet<GateEvent> GateEvents => Set<GateEvent>();

        public DbSet<Justification> Justifications => Set<Justification>();

        public DbSet<LeavePass> LeavePasses => Set<LeavePass>();

        public DbSet<GradingScale> GradingScales => Set<GradingScale>();

        public DbSet<ScaleBand> ScaleBands => Set<ScaleBand>();

        public DbSet<Blueprint> Blueprints => Set<Blueprint>();

        public DbSet<BlueprintComponent> BlueprintComponents => Set<BlueprintComponent>();

        public DbSet<Marksheet> Marksheets => Set<Marksheet>();

        public DbSet<MarkEntry> MarkEntries => Set<MarkEntry>();

        public DbSet<TermResult> TermResults => Set<TermResult>();

        public DbSet<FeeCategory> FeeCategories => Set<FeeCategory>();

        public DbSet<FeeStructureLine> FeeStructureLines => Set<FeeStructureLine>();

        public DbSet<Payer> Payers => Set<Payer>();

        public DbSet<Charge> Charges => Set<Charge>();

        public DbSet<CreditNote> CreditNotes => Set<CreditNote>();

        public DbSet<TillSession> TillSessions => Set<TillSession>();

        public DbSet<Receipt> Receipts => Set<Receipt>();

        public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

        public DbSet<Pdc> Pdcs => Set<Pdc>();

        public DbSet<RefundVoucher> RefundVouchers => Set<RefundVoucher>();

        public DbSet<TimetableShape> TimetableShapes => Set<TimetableShape>();

        public DbSet<PeriodSlot> PeriodSlots => Set<PeriodSlot>();

        public DbSet<TimetableVersion> TimetableVersions => Set<TimetableVersion>();

        public DbSet<Placement> Placements => Set<Placement>();

        public DbSet<Session> Sessions => Set<Session>();

        public DbSet<Substitution> Substitutions => Set<Substitution>();

        public DbSet<ExamType> ExamTypes => Set<ExamType>();

        public DbSet<ExamRound> ExamRounds => Set<ExamRound>();

        public DbSet<Exam> Exams => Set<Exam>();

        public DbSet<ExamSitting> ExamSittings => Set<ExamSitting>();

        public DbSet<ExamAttendance> ExamAttendances => Set<ExamAttendance>();

        public DbSet<ExamIncident> ExamIncidents => Set<ExamIncident>();

        public DbSet<MakeupEligibility> MakeupEligibilities => Set<MakeupEligibility>();

        public DbSet<PromotionCriteria> PromotionCriteria => Set<PromotionCriteria>();

        public DbSet<YearResult> YearResults => Set<YearResult>();

        public DbSet<CertificateType> CertificateTypes => Set<CertificateType>();

        public DbSet<CertificateRequest> CertificateRequests => Set<CertificateRequest>();

        public DbSet<CertificateIssue> CertificateIssues => Set<CertificateIssue>();

        public DbSet<VerificationLog> VerificationLogs => Set<VerificationLog>();

        public DbSet<PlanTemplate> PlanTemplates => Set<PlanTemplate>();

        public DbSet<TemplateInstallment> TemplateInstallments => Set<TemplateInstallment>();

        public DbSet<PlanAssignment> PlanAssignments => Set<PlanAssignment>();

        public DbSet<Installment> Installments => Set<Installment>();

        public DbSet<InstallmentChargeLine> InstallmentChargeLines => Set<InstallmentChargeLine>();

        public DbSet<ScheduleRevision> ScheduleRevisions => Set<ScheduleRevision>();

        public DbSet<RescheduleCase> RescheduleCases => Set<RescheduleCase>();

        public DbSet<PromiseToPay> PromisesToPay => Set<PromiseToPay>();

        public DbSet<DunningEvent> DunningEvents => Set<DunningEvent>();

        public DbSet<DiscountType> DiscountTypes => Set<DiscountType>();

        public DbSet<EligibilityRule> EligibilityRules => Set<EligibilityRule>();

        public DbSet<DiscountGrant> DiscountGrants => Set<DiscountGrant>();

        public DbSet<DiscountDocument> DiscountDocuments => Set<DiscountDocument>();

        public DbSet<ScholarshipProgram> ScholarshipPrograms => Set<ScholarshipProgram>();

        public DbSet<Waiver> Waivers => Set<Waiver>();

        public DbSet<RenewalQueueItem> RenewalQueueItems => Set<RenewalQueueItem>();

        public DbSet<StatementIssue> StatementIssues => Set<StatementIssue>();

        public DbSet<GlAccountMapping> GlAccountMappings => Set<GlAccountMapping>();

        public DbSet<GlExportBatch> GlExportBatches => Set<GlExportBatch>();

        public DbSet<GlJournalLine> GlJournalLines => Set<GlJournalLine>();

        public DbSet<Bus> Buses => Set<Bus>();

        public DbSet<BusDocument> BusDocuments => Set<BusDocument>();

        public DbSet<TransportStaff> TransportStaff => Set<TransportStaff>();

        public DbSet<Route> Routes => Set<Route>();

        public DbSet<RouteStop> RouteStops => Set<RouteStop>();

        public DbSet<TransportSubscription> TransportSubscriptions => Set<TransportSubscription>();

        public DbSet<RouteWaitlist> RouteWaitlists => Set<RouteWaitlist>();

        public DbSet<Trip> Trips => Set<Trip>();

        public DbSet<TripLog> TripLogs => Set<TripLog>();

        public DbSet<SafetyEvent> SafetyEvents => Set<SafetyEvent>();

        public DbSet<MedicalFile> MedicalFiles => Set<MedicalFile>();

        public DbSet<Allergy> Allergies => Set<Allergy>();

        public DbSet<MedicalCondition> MedicalConditions => Set<MedicalCondition>();

        public DbSet<CarePlan> CarePlans => Set<CarePlan>();

        public DbSet<ClinicVisit> ClinicVisits => Set<ClinicVisit>();

        public DbSet<MedicationAuthorization> MedicationAuthorizations => Set<MedicationAuthorization>();

        public DbSet<AdministrationLog> AdministrationLogs => Set<AdministrationLog>();

        public DbSet<VaccinationScheduleEntry> VaccinationScheduleEntries => Set<VaccinationScheduleEntry>();

        public DbSet<VaccinationRecord> VaccinationRecords => Set<VaccinationRecord>();

        public DbSet<VaccinationCampaign> VaccinationCampaigns => Set<VaccinationCampaign>();

        public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

        public DbSet<ScreeningCampaign> ScreeningCampaigns => Set<ScreeningCampaign>();

        public DbSet<ScreeningResult> ScreeningResults => Set<ScreeningResult>();

        public DbSet<InfectiousCase> InfectiousCases => Set<InfectiousCase>();

        public DbSet<ExposureNotice> ExposureNotices => Set<ExposureNotice>();

        public DbSet<BehaviorCode> BehaviorCodes => Set<BehaviorCode>();

        public DbSet<ViolationType> ViolationTypes => Set<ViolationType>();

        public DbSet<MeritType> MeritTypes => Set<MeritType>();

        public DbSet<ConsequenceType> ConsequenceTypes => Set<ConsequenceType>();

        public DbSet<LadderStep> LadderSteps => Set<LadderStep>();

        public DbSet<Incident> Incidents => Set<Incident>();

        public DbSet<Merit> Merits => Set<Merit>();

        public DbSet<DisciplineCase> DisciplineCases => Set<DisciplineCase>();

        public DbSet<CaseStatement> CaseStatements => Set<CaseStatement>();

        public DbSet<ActionApplied> ActionsApplied => Set<ActionApplied>();

        public DbSet<Appeal> Appeals => Set<Appeal>();

        public DbSet<PointLedgerEntry> PointLedgerEntries => Set<PointLedgerEntry>();

        public DbSet<BehaviorContract> BehaviorContracts => Set<BehaviorContract>();

        public DbSet<KeepApartPair> KeepApartPairs => Set<KeepApartPair>();

        public DbSet<ParentMeeting> ParentMeetings => Set<ParentMeeting>();

        public DbSet<Title> Titles => Set<Title>();

        public DbSet<Copy> Copies => Set<Copy>();

        public DbSet<MemberPolicy> MemberPolicies => Set<MemberPolicy>();

        public DbSet<Loan> Loans => Set<Loan>();

        public DbSet<CirculationEvent> CirculationEvents => Set<CirculationEvent>();

        public DbSet<Reservation> Reservations => Set<Reservation>();

        public DbSet<FineProposal> FineProposals => Set<FineProposal>();

        public DbSet<StocktakeSession> StocktakeSessions => Set<StocktakeSession>();

        public DbSet<StocktakeLine> StocktakeLines => Set<StocktakeLine>();

        public DbSet<ReadingLog> ReadingLogs => Set<ReadingLog>();

        public DbSet<CafeteriaItem> CafeteriaItems => Set<CafeteriaItem>();

        public DbSet<Menu> Menus => Set<Menu>();

        public DbSet<MenuLine> MenuLines => Set<MenuLine>();

        public DbSet<Wallet> Wallets => Set<Wallet>();

        public DbSet<WalletLedgerEntry> WalletLedgerEntries => Set<WalletLedgerEntry>();

        public DbSet<SpendControl> SpendControls => Set<SpendControl>();

        public DbSet<Sale> Sales => Set<Sale>();

        public DbSet<SaleLine> SaleLines => Set<SaleLine>();

        public DbSet<MealPlan> MealPlans => Set<MealPlan>();

        public DbSet<MealPlanSubscription> MealPlanSubscriptions => Set<MealPlanSubscription>();

        public DbSet<Redemption> Redemptions => Set<Redemption>();

        public DbSet<StockMovement> StockMovements => Set<StockMovement>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // Base runs last so tenant/soft-active filters cover every entity
            // the configurations added.
            base.OnModelCreating(modelBuilder);
        }
    }
}
