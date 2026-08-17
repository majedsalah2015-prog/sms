using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Hangfire;
using Sms.Application.Admissions;
using Sms.Application.Attachments;
using Sms.Application.Attendance;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Certificates;
using Sms.Application.Classrooms;
using Sms.Application.Common.Interfaces;
using Sms.Application.Employees;
using Sms.Application.Examinations;
using Sms.Application.Fees;
using Sms.Application.GlExport;
using Sms.Application.Grades;
using Sms.Application.Discounts;
using Sms.Application.Grading;
using Sms.Application.Health;
using Sms.Application.Installments;
using Sms.Application.Jobs;
using Sms.Application.Lookups;
using Sms.Application.Notifications;
using Sms.Application.Numbering;
using Sms.Application.Parents;
using Sms.Application.Payments;
using Sms.Application.Portal;
using Sms.Application.Schools;
using Sms.Application.Sections;
using Sms.Application.Security;
using Sms.Application.Statements;
using Sms.Application.Students;
using Sms.Application.Subjects;
using Sms.Application.Teachers;
using Sms.Application.Timetable;
using Sms.Application.Transport;
using Sms.Application.Workflow;
using Sms.Domain.Jobs;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Admissions;
using Sms.Infrastructure.Attachments;
using Sms.Infrastructure.Attendance;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Calendar;
using Sms.Infrastructure.Certificates;
using Sms.Infrastructure.Classrooms;
using Sms.Infrastructure.Common;
using Sms.Infrastructure.Employees;
using Sms.Infrastructure.Examinations;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.GlExport;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Discounts;
using Sms.Infrastructure.Grading;
using Sms.Infrastructure.Health;
using Sms.Infrastructure.Installments;
using Sms.Infrastructure.Jobs;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Parents;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Portal;
using Sms.Infrastructure.Schools;
using Sms.Infrastructure.Sections;
using Sms.Infrastructure.Security;
using Sms.Infrastructure.Statements;
using Sms.Infrastructure.Students;
using Sms.Infrastructure.Subjects;
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
            services.AddControllersWithViews();

            // E-002 tenancy + working-year context (ADR-2/3). Static single-tenant
            // wiring until M02/M03 provide school resolution and the year switcher.
            var tenant = new StaticTenantContext(
                Configuration.GetValue("Tenant:SchoolId", 1),
                Configuration.GetValue("Tenant:WorkingAcademicYearId", 1));
            services.AddSingleton<ITenantContext>(tenant);
            services.AddSingleton<IWorkingYearContext>(tenant);
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ICurrentUser, SystemUser>();

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

            // Stamp per-request audit metadata (doc 07 §4). IP capture for
            // portal users pends the country-pack privacy check (doc 07 Q2).
            app.Use(async (context, next) =>
            {
                var audit = context.RequestServices.GetRequiredService<IAuditContext>();
                audit.SourceScreen = context.Request.Path;
                audit.ClientIp = context.Connection.RemoteIpAddress?.ToString();
                await next();
            });

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
        }
    }
}
