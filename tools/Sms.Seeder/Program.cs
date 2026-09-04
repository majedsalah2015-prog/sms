using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Attendance;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Common.Interfaces;
using Sms.Application.Employees;
using Sms.Application.Cafeteria;
using Sms.Application.Fees;
using Sms.Application.Health;
using Sms.Application.Grades;
using Sms.Application.Lookups;
using Sms.Application.Dashboards;
using Sms.Application.Numbering;
using Sms.Application.Parents;
using Sms.Application.Payments;
using Sms.Application.Reports;
using Sms.Application.Schools;
using Sms.Application.Sections;
using Sms.Application.Security;
using Sms.Application.Notifications;
using Sms.Application.Seeding;
using Sms.Application.Setup;
using Sms.Application.Students;
using Sms.Application.Subjects;
using Sms.Application.Teachers;
using Sms.Infrastructure.Attendance;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Calendar;
using Sms.Infrastructure.Common;
using Sms.Infrastructure.Employees;
using Sms.Infrastructure.Cafeteria;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Health;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Dashboards;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Parents;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Reports;
using Sms.Infrastructure.Schools;
using Sms.Infrastructure.Security;
using Sms.Infrastructure.Notifications;
using Microsoft.Data.SqlClient;
using ERP2028.Infrastructure.Shared.Persistence;
using ERP2028.Modules.Accounting.Application.DependencyInjection;
using ERP2028.Modules.Accounting.Infrastructure.DependencyInjection;
using ERP2028.Modules.Organization.Application.DependencyInjection;
using ERP2028.Modules.Organization.Infrastructure.DependencyInjection;
using Sms.Application.GlExport;
using Sms.Infrastructure.GlExport;
using Sms.Erp.Bridge.DependencyInjection;
using Sms.Infrastructure.Seeding;
using Sms.Infrastructure.Setup;
using Sms.Infrastructure.Sections;
using Sms.Infrastructure.Students;
using Sms.Infrastructure.Subjects;
using Sms.Infrastructure.Teachers;

namespace Sms.Seeder
{
    /// <summary>
    /// The demo-tenant seeder harness (doc 02 §9, IP-02 §2 — the same
    /// fixture sales/QA/perf tests use). Registers every ISeedContributor
    /// and runs them in order; each is idempotent, so re-running against an
    /// already-seeded tenant is safe.
    /// </summary>
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection();

            var tenant = new StaticTenantContext(
                configuration.GetValue("Tenant:SchoolId", 1),
                configuration.GetValue("Tenant:WorkingAcademicYearId", 1));
            services.AddSingleton<ITenantContext>(tenant);
            services.AddSingleton<IWorkingYearContext>(tenant);
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ICurrentUser, SystemUser>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("Sms")
                    ?? "Server=(localdb)\\mssqllocaldb;Database=SmsDemoTenant;Trusted_Connection=True;"));
            services.AddScoped<IAuditContext, AuditContext>();

            // BR-NTF-003's credential protection. Nothing here writes a gateway token — the
            // seeder has none to write — but NotificationOpsAdmin takes the protector, and this
            // harness builds its own container, so the port has to be here or the run dies at
            // container build with a message about a service nobody in the seed cares about.
            services.AddDataProtection();
            services.AddHttpClient();
            services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();

            services.AddScoped<SmsDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IAuditEventWriter, AuditEventWriter>();
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();

            services.AddScoped<INumberingSeriesAdmin, NumberingSeriesAdmin>();
            services.AddScoped<ILookupAdmin, LookupAdmin>();
            services.AddScoped<INumberIssuer, NumberIssuer>();

            // S3/E-305 demo tenant - every admin service DemoSeedContributor composes.
            services.AddScoped<ISchoolAdmin, SchoolAdmin>();

            // The publisher snapshots the address each delivery went to (BR-NOT-008), so it
            // needs the address book even here, where nothing is actually sent: the seeder
            // composes admins that publish, and an unregistered port fails the whole run at
            // container build rather than at the one contributor that would have used it.
            services.AddScoped<IRecipientAddressBook, RecipientAddressBook>();
            services.AddScoped<INotificationPublisher, NotificationPublisher>();
            services.AddScoped<ISystemSetupAdmin, SystemSetupAdmin>();
            services.AddScoped<IAcademicYearAdmin, AcademicYearAdmin>();
            services.AddScoped<IGradeStructureAdmin, GradeStructureAdmin>();
            services.AddScoped<ISectionAdmin, SectionAdmin>();
            // SubjectAdmin grew a usage guard for curriculum offerings and the web host registers
            // it; without the same registration here the seeder cannot build its own container at
            // all, and fails before a single contributor runs.
            services.AddScoped<Sms.Application.Common.Guards.IUsageInspector<Sms.Domain.Subjects.CurriculumOffering>, CurriculumOfferingUsageInspector>();
            services.AddScoped<ISubjectAdmin, SubjectAdmin>();
            services.AddScoped<ICalendarAdmin, CalendarAdmin>();
            services.AddScoped<IEmployeeAdmin, EmployeeAdmin>();
            services.AddScoped<ITeacherAdmin, TeacherAdmin>();
            services.AddScoped<IParentAdmin, ParentAdmin>();
            services.AddScoped<IStudentAdmin, StudentAdmin>();
            services.AddScoped<IAttendanceAdmin, AttendanceAdmin>();
            services.AddScoped<IFeeAdmin, FeeAdmin>();
            services.AddScoped<ICafeteriaAdmin, CafeteriaAdmin>();
            services.AddScoped<IHealthAdmin, HealthAdmin>();
            services.AddScoped<INotificationPublisher, NotificationPublisher>();

            // E-701/E-702 registries — the report catalogue and the widget registry are
            // written through their own admin ports, same as every other seeded catalogue.
            services.AddScoped<IReportAdmin, ReportAdmin>();
            services.AddScoped<IDashboardAdmin, DashboardAdmin>();
            services.AddScoped<INotificationConfigAdmin, NotificationConfigAdmin>();

            services.AddScoped<ISeedContributor, JobDefinitionSeedContributor>();
            services.AddScoped<ISeedContributor, LookupProductSeedContributor>();
            services.AddScoped<ISeedContributor, GeographySeedContributor>();
            services.AddScoped<ISeedContributor, RoleTemplateSeedContributor>();
            services.AddScoped<ISeedContributor, PermissionSeedContributor>();
            services.AddScoped<ISeedContributor, SysAdminAccountSeedContributor>();
            services.AddScoped<ISeedContributor, Ksa01ContentPackSeedContributor>();
            services.AddScoped<ISeedContributor, NumberingCatalogSeedContributor>();
            services.AddScoped<ISeedContributor, ReportCatalogSeedContributor>();
            services.AddScoped<ISeedContributor, WidgetRegistrySeedContributor>();
            services.AddScoped<ISeedContributor, WorkflowCatalogSeedContributor>();
            services.AddScoped<ISeedContributor, NotificationDefaultsSeedContributor>();

            // The wording behind those rules. Ordered after them by ISeedContributor.Order, not
            // by this line — but registered next to them so the pair is read as a pair: a rule
            // with no template notifies nobody, and a template with no rule is never consulted.
            services.AddScoped<INotificationOpsAdmin, NotificationOpsAdmin>();
            services.AddScoped<ISeedContributor, NotificationTemplateSeedContributor>();
            services.AddScoped<ISeedContributor, DemoSeedContributor>();
            services.AddScoped<ISeedContributor, StaffDemoSeedContributor>();
            services.AddScoped<ISeedContributor, PortalDemoAccountSeedContributor>();
            services.AddScoped<ISeedContributor, CafeteriaDemoSeedContributor>();

            // The cashier's destination picker. Its own contributor rather than a tail of the demo
            // tenant: that one returns early once a school exists, so these never reached a database
            // provisioned before the catalogue was built (BR-PAY-002).
            services.AddScoped<ICollectionAccountAdmin, CollectionAccountAdmin>();
            services.AddScoped<ISeedContributor, CollectionAccountDemoSeedContributor>();

            // The embedded ERP modules' permission names, catalogued as sec.Permission rows and
            // granted to SYSADMIN, so an administrator can reach the accounting screens and hand the
            // access on through the ordinary role screen.
            services.AddErpPermissionCatalog();
            services.AddScoped<ISeedContributor, ExternalPermissionSeedContributor>();

            // The GL mapping table, filled from the ERP's chart of accounts. This needs the Accounting
            // module itself resolvable — the provisioner creates accounts through Accounting's own
            // write path (IChartOfAccountsProvisioning), never by touching acc.Accounts — so the module
            // is registered here exactly as the web host registers it, on a shared MARS connection.
            //
            // AddHttpContextAccessor in a console tool looks odd but is correct: the ERP's ICurrentUser
            // reads an HTTP context, finds none here, and reports an unauthenticated user, which is
            // what a seeder is. The ledger stamps "system" for it.
            var erpConnectionString = configuration.GetConnectionString("Sms");
            if (!string.IsNullOrWhiteSpace(erpConnectionString))
            {
                services.AddHttpContextAccessor();
                services.AddSharedRequestConnection(() => new SqlConnection(
                    new SqlConnectionStringBuilder(erpConnectionString) { MultipleActiveResultSets = true }.ConnectionString));
                services.AddErpHostAdapters();
                services.AddOrganizationApplication();
                services.AddOrganizationInfrastructure(erpConnectionString);
                services.AddAccountingApplication();
                services.AddAccountingInfrastructure(erpConnectionString);

                services.AddScoped<IGlExportService, GlExportService>();
                services.AddScoped<ISeedContributor, GlAccountMappingSeedContributor>();
            }

            services.AddScoped<SeedRunner>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            if (args.Length > 0 && args[0].Equals("gl-export", StringComparison.OrdinalIgnoreCase))
            {
                await RunGlExportAsync(scope.ServiceProvider, args);
                return;
            }

            if (args.Length > 0 && args[0].Equals("reset-password", StringComparison.OrdinalIgnoreCase))
            {
                await ResetPasswordAsync(scope.ServiceProvider, args);
                return;
            }

            var runner = scope.ServiceProvider.GetRequiredService<SeedRunner>();
            var ran = await runner.RunAllAsync();

            foreach (var name in ran)
            {
                Console.WriteLine($"Seeded: {name}");
            }
        }

        /// <summary>
        /// <c>reset-password &lt;userName&gt; &lt;newPassword&gt;</c> — issues a one-time
        /// credential for an account that has lost its way in.
        /// <para>
        /// The escape hatch a self-hosted product needs: password reset is an
        /// administrator screen, and an administrator locked out of the only
        /// administrator account cannot reach it. Physical access to the database
        /// server is the authority here, which is the same authority that could
        /// edit <c>sec.UserAccount</c> directly — this only makes it survivable
        /// instead of destructive.
        /// </para>
        /// <para>
        /// It goes through <c>SetTemporaryPasswordAsync</c>, the same path the
        /// bootstrap seeder uses, so the value is one-time by construction:
        /// BR-SEC-005 forces a change before the account can do anything else.
        /// A password handed over in the clear is only safe because of that, so
        /// do not replace this with a plain hash write.
        /// </para>
        /// </summary>
        private static async Task ResetPasswordAsync(IServiceProvider services, string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: reset-password <userName> <newPassword>");
                return;
            }

            var db = services.GetRequiredService<AppDbContext>();
            var auth = services.GetRequiredService<IAuthenticationService>();

            var account = await db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.UserName == args[1]);
            if (account == null)
            {
                Console.WriteLine($"No account named '{args[1]}'.");
                return;
            }

            await auth.SetTemporaryPasswordAsync(account.Id, args[2]);
            Console.WriteLine($"'{account.UserName}' now has a one-time password; BR-SEC-005 forces a change at the next sign-in.");
        }

        /// <summary>
        /// <c>gl-export &lt;from&gt; &lt;to&gt;</c> — generates the journal batch for a
        /// period and, when a ledger is attached, posts it.
        /// <para>
        /// Module 19 §8's GL export screen is still deferred, so until it lands
        /// this is the only way to run a period. It lives in the seeder because
        /// that is the one host already composing both this system and the ERP's
        /// Accounting module; a second console project to hold one command would
        /// be worse. It is an operator command, not part of seeding, and the
        /// seeder does not run when it is used.
        /// </para>
        /// </summary>
        private static async Task RunGlExportAsync(IServiceProvider services, string[] args)
        {
            if (args.Length < 3
                || !DateTime.TryParse(args[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var from)
                || !DateTime.TryParse(args[2], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var to))
            {
                Console.WriteLine("Usage: gl-export <from yyyy-MM-dd> <to yyyy-MM-dd>");
                return;
            }

            var glExport = services.GetService<IGlExportService>();
            if (glExport == null)
            {
                Console.WriteLine("GL export is not available: no connection string is configured.");
                return;
            }

            // End of day, so a document posted on the closing date is inside the period rather than
            // just outside it.
            to = to.Date.AddDays(1).AddTicks(-1);

            var batch = await glExport.GenerateAsync(from, to, generatedByUserId: 0);

            Console.WriteLine($"Batch      : {batch.BatchNo}");
            Console.WriteLine($"Period     : {batch.PeriodFromUtc:yyyy-MM-dd} .. {batch.PeriodToUtc:yyyy-MM-dd}");
            Console.WriteLine($"Documents  : {batch.SourceDocumentCount}");
            Console.WriteLine($"Debit      : {batch.TotalDebit:N2}");
            Console.WriteLine($"Credit     : {batch.TotalCredit:N2}");
            Console.WriteLine($"Ledger     : {batch.PostedJournalNo ?? "(not posted — no ledger attached)"}");

            foreach (var line in batch.Lines.OrderBy(l => l.SequenceNumber))
            {
                Console.WriteLine($"  {line.AccountCode,-10} Dr {line.Debit,12:N2}  Cr {line.Credit,12:N2}  {line.AccountKey}");
            }
        }
    }
}
