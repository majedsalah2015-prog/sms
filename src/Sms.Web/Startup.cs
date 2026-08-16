using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Hangfire;
using Sms.Application.Attachments;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Common.Interfaces;
using Sms.Application.Grades;
using Sms.Application.Jobs;
using Sms.Application.Lookups;
using Sms.Application.Notifications;
using Sms.Application.Numbering;
using Sms.Application.Parents;
using Sms.Application.Schools;
using Sms.Application.Sections;
using Sms.Application.Security;
using Sms.Application.Students;
using Sms.Application.Subjects;
using Sms.Application.Workflow;
using Sms.Domain.Jobs;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Attachments;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Calendar;
using Sms.Infrastructure.Common;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Jobs;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Parents;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Schools;
using Sms.Infrastructure.Sections;
using Sms.Infrastructure.Security;
using Sms.Infrastructure.Students;
using Sms.Infrastructure.Subjects;
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
