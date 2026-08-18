using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Sms.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Hangfire;
using Sms.Application.Activities;
using Sms.Application.Admissions;
using Sms.Application.Attachments;
using Sms.Application.Attendance;
using Sms.Application.Setup;
using Sms.Application.Audit;
using Sms.Application.Backup;
using Sms.Application.Cafeteria;
using Sms.Application.Calendar;
using Sms.Application.Certificates;
using Sms.Application.Classrooms;
using Sms.Application.Dashboards;
using Sms.Application.Common.Interfaces;
using Sms.Application.Employees;
using Sms.Application.Examinations;
using Sms.Application.Fees;
using Sms.Application.GlExport;
using Sms.Application.Grades;
using Sms.Application.Discipline;
using Sms.Application.Discounts;
using Sms.Application.Grading;
using Sms.Application.Health;
using Sms.Application.Installments;
using Sms.Application.Jobs;
using Sms.Application.Library;
using Sms.Application.Lookups;
using Sms.Application.Messaging;
using Sms.Application.Notifications;
using Sms.Application.Numbering;
using Sms.Application.Parents;
using Sms.Application.Payments;
using Sms.Application.Portal;
using Sms.Application.ReadModels;
using Sms.Application.Reports;
using Sms.Application.Rollover;
using Sms.Application.Schools;
using Sms.Application.Sections;
using Sms.Application.Security;
using Sms.Application.Statements;
using Sms.Application.Store;
using Sms.Application.Students;
using Sms.Application.Subjects;
using Sms.Application.SysAdmin;
using Sms.Application.Teachers;
using Sms.Application.Timetable;
using Sms.Application.Transport;
using Sms.Application.Workflow;
using Sms.Domain.Jobs;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Activities;
using Sms.Infrastructure.Admissions;
using Sms.Infrastructure.Attachments;
using Sms.Infrastructure.Attendance;
using Sms.Infrastructure.Setup;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Backup;
using Sms.Infrastructure.Cafeteria;
using Sms.Infrastructure.Calendar;
using Sms.Infrastructure.Certificates;
using Sms.Infrastructure.Classrooms;
using Sms.Infrastructure.Dashboards;
using Sms.Infrastructure.Common;
using Sms.Infrastructure.Employees;
using Sms.Infrastructure.Examinations;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.GlExport;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Discipline;
using Sms.Infrastructure.Discounts;
using Sms.Infrastructure.Grading;
using Sms.Infrastructure.Health;
using Sms.Infrastructure.Installments;
using Sms.Infrastructure.Jobs;
using Sms.Infrastructure.Library;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Messaging;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Parents;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Portal;
using Sms.Infrastructure.ReadModels;
using Sms.Infrastructure.Reports;
using Sms.Infrastructure.Rollover;
using Sms.Infrastructure.Schools;
using Sms.Infrastructure.Sections;
using Sms.Infrastructure.Security;
using Sms.Infrastructure.Statements;
using Sms.Infrastructure.Store;
using Sms.Infrastructure.Students;
using Sms.Infrastructure.Subjects;
using Sms.Infrastructure.SysAdmin;
using Sms.Infrastructure.Teachers;
using Sms.Infrastructure.Timetable;
using Sms.Infrastructure.Transport;
using Sms.Infrastructure.Workflow;

namespace Sms.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews(options =>
            {
                // BR-SEC-005: forced password change gates every other action.
                options.Filters.Add<RequirePasswordChangeFilter>();
            });

            // Login (doc 06 §3): cookie principal bound to a sec.UserSession row,
            // re-validated per request by SessionCookieEvents; a second, 5-minute
            // scheme carries the password-verified-awaiting-TOTP state.
            services.AddHttpContextAccessor();
            services.AddScoped<SessionCookieEvents>();
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = "Sms.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.SlidingExpiration = true;
                    options.EventsType = typeof(SessionCookieEvents);
                })
                .AddCookie("Sms.TwoFactor", options =>
                {
                    options.Cookie.Name = "Sms.TwoFactor";
                    options.Cookie.HttpOnly = true;
                    options.ExpireTimeSpan = System.TimeSpan.FromMinutes(5);
                });

            // Deny-by-default (doc 06 §1): every endpoint needs an authenticated
            // user unless explicitly [AllowAnonymous] (login, static assets).
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            });

            // E-002 tenancy + working-year context (ADR-2/3). Static single-tenant
            // wiring until M02/M03 provide school resolution and the year switcher.
            var tenant = new StaticTenantContext(
                Configuration.GetValue("Tenant:SchoolId", 1),
                Configuration.GetValue("Tenant:WorkingAcademicYearId", 1));
            services.AddSingleton<ITenantContext>(tenant);
            services.AddSingleton<IWorkingYearContext>(tenant);
            services.AddSingleton<IClock, SystemClock>();
            // ICurrentUser now resolves from the cookie principal (0 = system actor outside a request).
            services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

            // E-003 authorization core: deny-by-default policy engine (doc 06).
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("Sms")));
            services.AddScoped<IPermissionService, PermissionService>();

            // E-003 authentication slice (doc 06 §3, BR-SEC-001..004). The
            // cookie/session wiring that consumes this (login screen, real
            // ICurrentUser off the authenticated principal) is a later slice.
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();

            // E-004 audit framework (doc 07): capture runs inside the context;
            // these provide the ambient metadata, event API, and integrity ops.
            services.AddScoped<IAuditContext, AuditContext>();
            services.AddScoped<SmsDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IAuditEventWriter, AuditEventWriter>();
            services.AddScoped<IntegrityCheckpointService>();

            // E-005 workflow engine (doc 05): catalog runtime + approvals inbox.
            // Modules register their IWorkflowFinalEffect implementations here.
            services.AddScoped<IWorkflowService, WorkflowService>();
            services.AddScoped<IApprovalInboxQuery, ApprovalInboxQuery>();

            // E-006 numbering framework (doc 08): gap-free issuance for
            // strict/normal series + admin definition and cutover.
            services.AddScoped<INumberIssuer, NumberIssuer>();
            services.AddScoped<INumberingSeriesAdmin, NumberingSeriesAdmin>();

            // E-007 notifications core (doc 09): publish queues Deliveries atomically
            // with the business event; the dispatcher drains them through whichever
            // channel senders are registered. Email/SMS/WhatsApp are stub transports
            // pending a provider decision (doc 09 §9 Q1) — only In-App is live.
            services.AddScoped<INotificationPublisher, NotificationPublisher>();
            services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
            services.AddScoped<INotificationConfigAdmin, NotificationConfigAdmin>();
            services.AddScoped<IChannelSender, InAppChannelSender>();
            services.AddScoped<IChannelSender>(_ => new StubChannelSender(NotificationChannel.Email));
            services.AddScoped<IChannelSender>(_ => new StubChannelSender(NotificationChannel.Sms));
            services.AddScoped<IChannelSender>(_ => new StubChannelSender(NotificationChannel.WhatsApp));

            // E-008 attachments core (doc 10): typed upload/version pipeline with a
            // mandatory scan gate. No virus-scan vendor/ICAP adapter chosen yet
            // (doc 10 §9 Q3) — NullVirusScanner always reports Clean so the
            // quarantine pipeline is exercised without overclaiming real scanning.
            var attachmentsRoot = Path.Combine(Configuration.GetValue("Attachments:RootPath", Path.Combine(Path.GetTempPath(), "sms-attachments")));
            services.AddSingleton<IFileStore>(new LocalDiskFileStore(attachmentsRoot));
            services.AddSingleton<IVirusScanner, NullVirusScanner>();
            services.AddScoped<IAttachmentService, AttachmentService>();
            services.AddScoped<IAttachmentTypeAdmin, AttachmentTypeAdmin>();

            // E-010 lookup framework (BR-SET-001/002/007). The seeder harness
            // (SeedRunner + ISeedContributor implementations) is registered in
            // the standalone Sms.Seeder tool, not here — seeding must never run
            // as a side effect of the web app starting.
            services.AddScoped<ILookupAdmin, LookupAdmin>();

            // E-011 background jobs (doc 02 T-6, Hangfire per IP-02 §2). Every
            // recurring job calls IJobRunner — the single path that records
            // JobRun history and an AuditAction.JobRun event; Hangfire itself
            // never touches business logic directly. The job admin surface
            // (WBS) is Hangfire's own dashboard (wired below), not a custom
            // screen.
            services.AddScoped<IJobRunner, JobRunner>();
            services.AddScoped<IJobDefinitionAdmin, JobDefinitionAdmin>();
            services.AddScoped<IJobHandler, AuditCheckpointJobHandler>();
            services.AddScoped<IJobHandler, NotificationDispatchJobHandler>();

            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(Configuration.GetConnectionString("Sms")));
            services.AddHangfireServer();

            // E-102: School module (doc/Modules/02, BR-SCH-001..008) + Academic
            // Years (doc/Modules/03, BR-AYR-001..010). ITenantContext/
            // IWorkingYearContext still resolve from config (StaticTenantContext,
            // E-002) — wiring them to real School/AcademicYear rows (multi-school
            // resolution, per-session year switching) is follow-up work.
            services.AddScoped<ISchoolAdmin, SchoolAdmin>();

            // E-101 System Setup (doc/Modules/01): country packs, effective-dated
            // settings, feature toggles, setup wizard. IFeatureGate is the same
            // instance — the shell sidebar reads it (BR-SET-006).
            services.AddScoped<SystemSetupAdmin>();
            services.AddScoped<ISystemSetupAdmin>(sp => sp.GetRequiredService<SystemSetupAdmin>());
            services.AddScoped<IFeatureGate>(sp => sp.GetRequiredService<SystemSetupAdmin>());
            services.AddScoped<IAcademicYearAdmin, AcademicYearAdmin>();

            // E-103 (slice: Calendar, doc/Modules/04, BR-CAL-001..008). Impact
            // review on edits touching existing attendance/exam data (BR-CAL-004)
            // isn't enforced yet — Attendance/Examinations don't exist.
            services.AddScoped<ICalendarAdmin, CalendarAdmin>();

            // E-103 (slice: Grades, doc/Modules/05, BR-GRD-001..009). Promotion-
            // path validation (acyclic/complete, BR-GRD-002/009) is exposed as a
            // pure validator but not yet wired into AcademicYearAdmin.ActivateAsync
            // — that cross-module integration is follow-up work.
            services.AddScoped<IGradeStructureAdmin, GradeStructureAdmin>();

            // E-103 (slice: Sections, doc/Modules/06, BR-SCN-001..007). SectionMembership.EnrollmentId
            // got its real FK once E-202 added ppl.Enrollment.
            services.AddScoped<ISectionAdmin, SectionAdmin>();

            // E-104 (slice: Subjects, doc/Modules/07, BR-SUB-001..008).
            services.AddScoped<ISubjectAdmin, SubjectAdmin>();

            // S2/E-202 (slice: Students + Parents, doc/Modules/10-11). Both admin
            // services issue permanent numbers via E-006's INumberIssuer (series
            // STU/PAR, already seeded by E-010). Dedup engine, merge tool, and
            // WF-03 withdrawal clearance workflow are deferred.
            services.AddScoped<IStudentAdmin, StudentAdmin>();
            services.AddScoped<IParentAdmin, ParentAdmin>();

            // E-104 (slice: Classrooms, doc/Modules/08, BR-ROM-001..008) — E-104 is
            // now fully done (Subjects + Classrooms). Section.DefaultClassroomId got
            // its real FK to core.Room in this slice.
            services.AddScoped<IRoomAdmin, RoomAdmin>();

            // S2/E-201 (Admissions, doc/Modules/09, BR-ADM-001..011). RegisterAsync
            // composes IStudentAdmin + ISectionAdmin under one explicit transaction
            // (BR-ADM-007). Parent dedup, offer/expiry sweep, application fee, and
            // WF-01 approval-authority enforcement are deferred.
            services.AddScoped<IAdmissionAdmin, AdmissionAdmin>();

            // S2/E-203 (Employees + Teachers, doc/Modules/12-13, BR-EMP-001..004/
            // BR-TCH-001/002/004/005). Salary fields are plain columns, not SQL
            // Server Always Encrypted (O10) — no SQL Server instance exists in
            // this environment, flagged explicitly rather than faked. Staff
            // attendance, leave (WF-10), payroll-prep export, and offboarding
            // clearance are deferred entirely.
            services.AddScoped<IEmployeeAdmin, EmployeeAdmin>();
            services.AddScoped<ITeacherAdmin, TeacherAdmin>();

            // S3/E-301 (Attendance, doc/Modules/14, BR-ATD-002/003/005/006/007).
            // Daily mode only - Period mode needs Module 15's timetable sessions,
            // which don't exist yet. Escalation thresholds, gate-event auto-flip
            // of AttendanceDay.Status, and WF-14's P2 approval routing (only the
            // mandatory-reason half is enforced, via the generic T1 audit
            // pipeline) are deferred.
            services.AddScoped<IAttendanceAdmin, AttendanceAdmin>();

            // S3/E-302 + S4/E-402 (Grading, doc/Modules/17, BR-GRA-001/003/005/
            // 006/007). Percentage-band scales only. Year aggregation/GPA/
            // promotion outcome (ComputeYearResultAsync) use the latest
            // TermResult per offering as a stand-in for full term-weighted
            // aggregation (BR-GRA-003's configurable term-weight scheme isn't
            // implemented). WF-08 (CorrectPublishedMarksheetAsync) reopens a
            // Published marksheet to Draft, reason mandatory. Report-card PDF
            // rendering still needs the O6 engine decision (open); transcripts,
            // appeals, and comment banks remain deferred.
            services.AddScoped<IGradingAdmin, GradingAdmin>();

            // S4/E-402 (Examinations, doc/Modules/16, BR-EXM-002..004/006/008).
            // Marks capture reuses IGradingAdmin's Marksheet/MarkEntry directly
            // (doc's "single marks store") - this service owns scheduling,
            // seating, exam-day attendance, incidents, and makeup eligibility.
            // RecordExamAttendanceAsync writes real cross-module MarkEntry zeros
            // for unexcused absence per policy. Invigilation duty rosters
            // (BR-EXM-005) are deferred.
            services.AddScoped<IExaminationAdmin, ExaminationAdmin>();

            // S4/E-403 (Certificates, doc/Modules/18, BR-CRT-001..010). WF-09
            // prerequisite checks (published results / fee clearance) are real -
            // reuse E-302's TermResult and E-303's IFeeAdmin.ComputeStudentPositionAsync
            // directly. BR-CRT-008's country-pack legal gate ships as the
            // CertificateWithholdingPolicy constant (KSA-01: TC never fee-gated,
            // PROVISIONAL pending the doc's Q1 legal review) since no CountryPack
            // entity exists (E-101 never started); FeeClearanceRule.NoOverdue is
            // refused at definition time because Charge carries no due date.
            // Generation is atomic with real doc 08 numbering
            // (CertificateType.NumberingSeriesCode selects the series per type -
            // CERT/TC/etc, both already seeded by E-010). PDF rendering still
            // needs the O6 engine decision (open); employee service-certificate
            // and report-card official-copy registration through this same
            // engine (the doc's "one register for everything official") aren't
            // wired - both source modules already deferred their side of it.
            services.AddScoped<ICertificateAdmin, CertificateAdmin>();

            // S5/E-501 (Installment Plans + PDC lifecycle, doc/Modules/20,
            // BR-INS-001..010). Installment status is DERIVED from Module 21
            // allocations + dates on every read (BR-INS-007) - never stored.
            // Due dates shift off non-working days via CalendarDayResolver;
            // callers pass weekend days (School still has no weekend-day
            // config field). PDC coverage suppresses dunning; PaymentAdmin
            // un-covers on bounce. Dunning ladder timings are the doc's
            // proposed defaults (its own Q1) and publish through E-007's
            // INotificationPublisher (InstallmentDueSoon/InstallmentOverdue -
            // no templates seeded yet, so nothing is delivered until a school
            // writes them). Deferred: default-template-per-grade config, late
            // fees (Module 19 policy), service-suspension list (Q2, legal),
            // Hangfire scheduling of RunDunningAsync, portal screens.
            services.AddScoped<IInstallmentAdmin, InstallmentAdmin>();

            // S5/E-502 (Discounts + statements, doc/Modules/22, BR-DIS-001..010).
            // Discount documents are a distinct document type (numbered "DSC")
            // that every position reader subtracts alongside credit notes -
            // BR-DIS-010 forbids netting them invisibly. Approval routes by
            // BR-DIS-003 thresholds (recorded as ApprovalTier, chain not
            // routed); sibling eligibility reads StudentGuardianLink families;
            // staff eligibility bridges Parent<->Employee via UserAccountId (the
            // known identity-bridging seam). Waivers materialize as E-303
            // credit notes; approvals recompute E-501 schedules. Statements
            // ("STM") separate gross / discounts / credit notes / payments.
            services.AddScoped<IDiscountAdmin, DiscountAdmin>();
            services.AddScoped<IStatementService, StatementService>();

            // S5/E-503 (GL journal-summary export, O3 assumption per
            // Implementation 01): generic CSV export over a per-school mapping
            // table (GlAccountMapping keyed by GlAccountKeys + FeeCategory.
            // GlExportCode for revenue). No named accounting-system adapter -
            // the pilot school's ERP decides the first one. Batches are
            // balanced by construction, numbered "GLX", hashed, and may not
            // overlap a non-voided batch.
            services.AddScoped<IGlExportService, GlExportService>();

            // S6/E-601 (Transportation, doc/Modules/23, BR-TRN-001..009).
            // Roadworthiness and trip rosters are derived, never stored; trip
            // open enforces bus documents + driver licence class; trip close
            // enforces every roster student resolved + "bus empty" sweep.
            // Subscription posts the zone-priced transport charge through
            // E-303 (structure line per zone category); pro-ration (BR-FEE-006)
            // is still E-303's deferral. Not-boarded/route-change/suspension
            // notifications publish through E-007 (no templates seeded).
            // Deferred: attendant mandatory per pack (doc Q1), parent
            // "not riding today" portal declaration (Q3), Hangfire scheduling
            // of EscalateUnclosedTripsAsync, all doc Sec.8 screens.
            services.AddScoped<ITransportAdmin, TransportAdmin>();

            // S6/E-602 (Health, doc/Modules/24, BR-HLT-001..010). The medical
            // file is T0 read-audited via an explicit AuditAction.View event on
            // every full-file open; the emergency banner is the nurse-curated
            // denormalized subset read without opening the file. Sent-home
            // needs a verified pickup-authorized person (BR-PAR-008) or a
            // documented exception; medication administration only within the
            // authorization (deviation = reason mandatory); vaccination
            // campaigns need per-student consent (hard); infectious cases can
            // pre-capture MedicalLeave via E-301; exposure notices are
            // Principal-approved and anonymized. Vaccination schedule is a
            // per-school table (no CountryPack entity). Deferred: session-
            // teacher auto-notification on visit, referral letters through
            // the Module 18 register, counseling notes (doc Q3 out), all screens.
            services.AddScoped<IHealthAdmin, HealthAdmin>();

            // S6/E-603 (Discipline, doc/Modules/25, BR-DCP-001..010). Year-
            // versioned behavior code (types, consequence catalog - corporal
            // punishment not representable - and the severity x repetition
            // ladder); severity 1 resolves teacher-level, >= 2 opens a WF-11
            // case; decisions cite an article, need statements at severity
            // >= 3, a reason below the ladder proposal and the Principal above
            // it / for suspension-class / severity 4; suspension days capped by
            // the code's pack limit; one appeal within the window reviewed by a
            // non-decider; points ledger + flags; parent view masks the
            // reporter (BR-DCP-010). Country-pack starter code (doc Q1) not
            // seeded; Sections-balancing consumption of keep-apart pairs and
            // Module 17 conduct-grade wiring deferred; all screens deferred.
            services.AddScoped<IDisciplineAdmin, DisciplineAdmin>();

            // S6/E-604 (Library, doc/Modules/26, BR-LIB-001..009). Members are
            // Student/Employee ids directly; policies per member class x stage;
            // checkout gates (available copy, loan limit, unpaid library fines)
            // with a logged librarian override; due dates shift off non-working
            // days via CalendarDayResolver; FIFO reservation queue with hold
            // window; overdue notices + optional fines (doc Q1: off by default)
            // batch-confirmed into Module 19 misc charges (students only - staff
            // have no payer model); lost/found with credit-note reversal;
            // stocktake sessions; class-visit batch issue. Clearance status is
            // exposed for WF-03/BR-EMP-008 checklists (not wired - neither
            // checklist exists). All doc Sec.8 screens deferred.
            services.AddScoped<ILibraryAdmin, LibraryAdmin>();

            // S6/E-605 (Cafeteria, doc/Modules/27, BR-CAF-001..009). Wallet
            // balance = ledger sum; top-ups are Module 21 receipts with
            // Purpose = WalletTopUp (excluded from fee allocation / advance /
            // statements; journaled to WalletLiability by E-503); POS applies
            // parent spend controls + the Module 24 emergency-banner allergy
            // feed in real time (warn by default, hard-block on parent opt-in);
            // plan-first / wallet / cash tenders (cash needs an open Module 21
            // till session); stock deduct guard; same-session voids (T1 reason);
            // wallet refunds are Module 21 refund vouchers. Deferred: portal
            // top-up (gateway dormant), offline-queue sync mechanics (capture
            // time only), barcode ID cards (doc Q1), pack nutrition lists (Q4),
            // meal-plan pro-ration/unredeemed credit job, all screens.
            services.AddScoped<ICafeteriaAdmin, CafeteriaAdmin>();

            // S6/E-606 (School Store, doc/Modules/28, BR-STO-001..008). Items with
            // variants and versioned price lists (no POS overrides); every sale is
            // a Module 19 charge - cash/card add a Module 21 receipt allocated to
            // that charge (open till session), wallet debits the cafeteria ledger,
            // account-charge is category/cap-gated with a Finance override; bundles
            // per grade-year assigned+charged in batch, handed out per line with
            // sizes and e-ack (pay-first gate per doc Q2), undistributed-paid
            // visible and credited at withdrawal (BR-STO-007); returns/exchanges
            // per category policy; perpetual stock + reorder report. Deferred:
            // anonymous walk-in payer, mixed-category VAT split per basket
            // (first item's category wins - flagged), store stocktake sessions,
            // pre-orders, all screens.
            services.AddScoped<IStoreAdmin, StoreAdmin>();

            // S3/E-303 (Fees + Payments core, doc/Modules/19+21, BR-FEE-001..
            // 003/005/008, BR-PAY-001..005). Charge.InvoiceUuid/InvoiceHash
            // implement BR-FEE-005's e-invoicing-readiness fields for real
            // (ZATCA-style TLV QR payload + SHA-256 hash chain) - live
            // submission to a tax authority is out of scope. Pro-ration
            // (BR-FEE-006), late fees (BR-FEE-007), opening balances
            // (BR-FEE-009), discounts (Module 22), installments (Module 20),
            // bank reconciliation, and the online gateway (BR-PAY-007,
            // dormant per doc itself) are all deferred.
            services.AddScoped<IFeeAdmin, FeeAdmin>();
            services.AddScoped<IPaymentAdmin, PaymentAdmin>();

            // S3/E-304 (Portal essentials, BR-SEC-010..013). Read-only aggregation
            // over Attendance/Grading/Fees; one requestingUserAccountId covers
            // both "parent views a linked child" and "student views own record".
            // Retrofit: Student/Parent both got a nullable UserAccountId bridge
            // field this slice needed (mirrors Employee.UserAccountId, E-203) -
            // no admin service provisions portal accounts yet (Module 36).
            // BR-SEC-010 (portal-vs-staff routing) and BR-SEC-013 (idle re-auth)
            // are web-layer concerns, deferred with every other epic's screens.
            // Announcements (read-only) are deferred entirely - no Messaging
            // module exists yet (M32, S6/S7).
            services.AddScoped<IParentPortalQuery, ParentPortalQuery>();

            // S4/E-401 (Timetable, doc/Modules/15, BR-TTB-001..009). Assisted-manual
            // v1 (no auto-generation solver, deliberately Future per the doc's own
            // scope). PublishAsync reuses E-103's CalendarDayResolver to generate
            // dated Session rows only on working days.
            services.AddScoped<ITimetableAdmin, TimetableAdmin>();

            // S6/E-607 (Activities, doc/Modules/29, BR-ACT-001..008). Costed
            // enrollment activation posts a real charge via E-303's IFeeAdmin
            // (BR-ACT-007); free programs never touch Fees. ActivityProgram/
            // ActivityTrip are named to avoid colliding with Sms.Web/Sms.Seeder's
            // own Program entry-point class and E-601's Transport.Trip entity
            // respectively (same collision-avoidance discipline as E-201's
            // AdmissionApplication). Venue/timetable conflict surfacing and
            // in-school attendance reconciliation are deferred.
            services.AddScoped<IActivityAdmin, ActivityAdmin>();

            // S7/E-702 (Dashboards, doc/Modules/31, BR-DSH-001/002/003/006/007).
            // Widget *content* (the consolidated widget->data-source->drill-path
            // spec) is Phase 9, out of scope - this is the registry/personalization
            // platform plus a handful of real widget computations (IDashboardQuery)
            // that each reuse their owning module's own calculator, so BR-DSH-002's
            // "one computation source" holds by construction rather than by
            // discipline. PersonalizeAsync server-enforces the permission check
            // (doc §9) via the same AssignmentSnapshot pattern as E-003's
            // PermissionService, but for an arbitrary target user.
            services.AddScoped<IDashboardAdmin, DashboardAdmin>();
            services.AddScoped<IDashboardQuery, DashboardQuery>();

            // S7/E-703 (Messaging + Notifications admin, doc/Modules/32+33,
            // BR-MSG-001/002/004, BR-NTF-001/002/004). Messaging (human-composed:
            // announcements/threads/official letters) is distinct from E-007's
            // system-generated Notifications per doc 09's own boundary note -
            // delivery itself still rides E-007's channel infrastructure, not
            // reimplemented. Thread/MessageThread avoids colliding with
            // System.Threading.Thread (same discipline as E-607's renames).
            // NotificationOpsAdmin extends E-007's existing Template/
            // SubscriptionRule/BudgetCounter entities with real operational
            // gates (test-send-before-publish, statutory floor, budget
            // threshold) rather than adding parallel entities. Provider
            // credentials/failover (BR-NTF-003) and the delivery ops queue
            // (BR-NTF-005) are deferred.
            services.AddScoped<IMessagingAdmin, MessagingAdmin>();
            services.AddScoped<INotificationOpsAdmin, NotificationOpsAdmin>();

            // S7/E-701 (Reports platform, doc/Modules/30, BR-RPT-001..006).
            // Report *content* (the 150+ catalog reports themselves) is
            // Phase 9, out of scope - this is the registry/run/subscribe
            // platform: BR-RPT-002 View-permission gate, BR-RPT-003
            // Export-permission + no-restricted-email-delivery gates,
            // BR-RPT-005 heavy-report queueing, BR-RPT-006 subscription
            // recipients must independently hold the report's permission.
            // Permission checks for an arbitrary target user reuse the
            // same hand-rolled AssignmentSnapshot pattern as E-702's
            // DashboardAdmin, since PermissionService only checks the
            // ambient ICurrentUser.
            services.AddScoped<IReportAdmin, ReportAdmin>();

            // S8/E-801 — Year-end rollover (doc/Modules/03 §4, WF-02 family,
            // BR-AYR-008/009, BR-FEE-009). Composes Grades/Students/Sections/
            // Fees/AcademicYear admins under per-student transactions so a
            // killed activation or carry-forward run resumes without
            // double-enrolling or double-posting. First real wiring of
            // E-103's PromotionPathValidator, E-402's PromotionCriteria/
            // YearResult, and BR-AYR-004/005's opening/closing checklists.
            // Deferred: rollover cockpit + all doc §8 screens, doc §12
            // notifications, WF-03 hand-off for "Not Re-registering",
            // FeeStructureLine lock at activation, waiting-list seat release.
            services.AddScoped<IRolloverAdmin, RolloverAdmin>();

            // S8/E-802 — DB/04 §4 read models: the "views" (IReadModelQuery,
            // each reusing the owning module's calculator — one computation
            // source) and the snapshot tables (rpt schema, AsOfUtc on every row)
            // refreshed by IJobHandlers on the ops.JobDefinition schedule. Heavy
            // reports/dashboard widgets read these, never the hot tables (NF-P5).
            // DB/04 §1 index prescriptions applied in the EF configurations;
            // §6 P95 gates live in Sms.Infrastructure.Tests/PerfGateTests
            // (indicative on Sqlite — re-measured on SQL Server at pilot, P4).
            services.AddScoped<IReadModelQuery, ReadModelQuery>();
            services.AddScoped<ISnapshotRefreshService, SnapshotRefreshService>();
            services.AddScoped<IJobHandler, AgedReceivablesSnapshotJobHandler>();
            services.AddScoped<IJobHandler, DailyAttendanceSummarySnapshotJobHandler>();
            services.AddScoped<IJobHandler, CollectionCalendarSnapshotJobHandler>();

            // S7/E-704 — Audit admin (M34) + Backup (M35) + SysAdmin (M36),
            // closing S7. Audit admin wraps IntegrityCheckpointService with
            // persisted verification runs and an Auditor disposition queue
            // over anomaly hits; a failed unresolved run freezes audit-data
            // purge (BR-AUM-001). Backup admin models the policy/run/
            // verification/snapshot/restore-case rules doc §3 defines —
            // actual backup artifact creation is infra tooling, out of
            // scope; TakeSnapshotAsync is the real cross-module hook
            // (BR-BAK-004) that SysAdminService calls before every import
            // commit and purge execution. SysAdminService (not "SysAdmin" —
            // avoids colliding with the Sms.Domain.SysAdmin/
            // Sms.Application.SysAdmin namespace leaf segment, same
            // discipline as E-201's AdmissionApplication) hosts a single
            // generic PurgeExecution reused for both BR-SYS-005 and
            // BR-AUM-005 rather than a parallel purge entity per data
            // class. License tiers (O5) land here: Essentials/Professional/
            // Enterprise aligned to this build's own S0-S3/S4-S6/S7 stage
            // boundaries (Sms.Domain.SysAdmin.LicenseTier) — enforcement
            // middleware per module is a deferred wiring point, same
            // "engine built, not wired" precedent as PromotionPathValidator.
            services.AddScoped<IAuditAdmin, AuditAdmin>();
            services.AddScoped<IBackupAdmin, BackupAdmin>();
            services.AddScoped<ISysAdmin, SysAdminService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // doc/DesignSystem/01: default en-US, Arabic (ar-SA) via the shell's
            // language toggle (culture cookie); one set of views flips direction.
            app.UseRequestLocalization(new RequestLocalizationOptions()
                .SetDefaultCulture("en-US")
                .AddSupportedCultures("en-US", "ar-SA")
                .AddSupportedUICultures("en-US", "ar-SA"));

            // Stamp per-request audit metadata (doc 07 §4). IP capture for
            // portal users pends the country-pack privacy check (doc 07 Q2).
            app.Use(async (context, next) =>
            {
                var audit = context.RequestServices.GetRequiredService<IAuditContext>();
                audit.SourceScreen = context.Request.Path;
                audit.ClientIp = context.Connection.RemoteIpAddress?.ToString();
                await next();
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            // E-011: Hangfire's built-in dashboard is the job admin surface (WBS)
            // — no custom screen. Access control on this route is deferred with
            // every other admin screen (doc 06 permission-gating).
            app.UseHangfireDashboard();

            // Recurring jobs call through IJobRunner by code — see doc comments
            // on JobRunner/JobDefinitionAdmin. The cron schedules mirror the
            // JobDefinition rows a real deployment seeds via IJobDefinitionAdmin;
            // duplicated here as the literal source Hangfire's scheduler reads.
            RecurringJob.AddOrUpdate<IJobRunner>(
                "AuditIntegrityCheckpoint",
                runner => runner.RunAsync("AuditIntegrityCheckpoint", JobTriggerType.Scheduled, default),
                "0 2 * * *"); // daily 02:00 UTC — matches IntegrityCheckpointService's one-day default period

            RecurringJob.AddOrUpdate<IJobRunner>(
                "NotificationDispatch",
                runner => runner.RunAsync("NotificationDispatch", JobTriggerType.Scheduled, default),
                "*/5 * * * *"); // every 5 minutes

            // S8/E-802 — DB/04 §4 snapshot refreshes ("Snapshots refresh via ops.JobDefinition schedules").
            RecurringJob.AddOrUpdate<IJobRunner>(
                SnapshotJobCodes.AgedReceivables,
                runner => runner.RunAsync(SnapshotJobCodes.AgedReceivables, JobTriggerType.Scheduled, default),
                "30 2 * * *"); // daily 02:30 UTC — D refresh class (RPT-FEE-004, finance donut)
            RecurringJob.AddOrUpdate<IJobRunner>(
                SnapshotJobCodes.DailyAttendanceSummary,
                runner => runner.RunAsync(SnapshotJobCodes.DailyAttendanceSummary, JobTriggerType.Scheduled, default),
                "*/15 4-12 * * *"); // C15 refresh class during the KSA school day (UTC)
            RecurringJob.AddOrUpdate<IJobRunner>(
                SnapshotJobCodes.CollectionCalendar,
                runner => runner.RunAsync(SnapshotJobCodes.CollectionCalendar, JobTriggerType.Scheduled, default),
                "45 2 * * *"); // daily 02:45 UTC — RPT-INS-001 cashflow forecast
        }
    }
}
