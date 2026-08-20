using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Attendance;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Common.Interfaces;
using Sms.Application.Employees;
using Sms.Application.Fees;
using Sms.Application.Grades;
using Sms.Application.Lookups;
using Sms.Application.Numbering;
using Sms.Application.Parents;
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
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Parents;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Schools;
using Sms.Infrastructure.Security;
using Sms.Infrastructure.Notifications;
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
            services.AddScoped<SmsDbContext>(sp => sp.GetRequiredService<AppDbContext>());
            services.AddScoped<IAuditEventWriter, AuditEventWriter>();
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();

            services.AddScoped<INumberingSeriesAdmin, NumberingSeriesAdmin>();
            services.AddScoped<ILookupAdmin, LookupAdmin>();
            services.AddScoped<INumberIssuer, NumberIssuer>();

            // S3/E-305 demo tenant - every admin service DemoSeedContributor composes.
            services.AddScoped<ISchoolAdmin, SchoolAdmin>();
            services.AddScoped<INotificationPublisher, NotificationPublisher>();
            services.AddScoped<ISystemSetupAdmin, SystemSetupAdmin>();
            services.AddScoped<IAcademicYearAdmin, AcademicYearAdmin>();
            services.AddScoped<IGradeStructureAdmin, GradeStructureAdmin>();
            services.AddScoped<ISectionAdmin, SectionAdmin>();
            services.AddScoped<ISubjectAdmin, SubjectAdmin>();
            services.AddScoped<ICalendarAdmin, CalendarAdmin>();
            services.AddScoped<IEmployeeAdmin, EmployeeAdmin>();
            services.AddScoped<ITeacherAdmin, TeacherAdmin>();
            services.AddScoped<IParentAdmin, ParentAdmin>();
            services.AddScoped<IStudentAdmin, StudentAdmin>();
            services.AddScoped<IAttendanceAdmin, AttendanceAdmin>();
            services.AddScoped<IFeeAdmin, FeeAdmin>();

            services.AddScoped<ISeedContributor, LookupProductSeedContributor>();
            services.AddScoped<ISeedContributor, RoleTemplateSeedContributor>();
            services.AddScoped<ISeedContributor, SysAdminAccountSeedContributor>();
            services.AddScoped<ISeedContributor, Ksa01ContentPackSeedContributor>();
            services.AddScoped<ISeedContributor, NumberingCatalogSeedContributor>();
            services.AddScoped<ISeedContributor, DemoSeedContributor>();
            services.AddScoped<ISeedContributor, PortalDemoAccountSeedContributor>();
            services.AddScoped<SeedRunner>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var runner = scope.ServiceProvider.GetRequiredService<SeedRunner>();
            var ran = await runner.RunAllAsync();

            foreach (var name in ran)
            {
                Console.WriteLine($"Seeded: {name}");
            }
        }
    }
}
