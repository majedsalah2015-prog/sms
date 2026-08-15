using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Lookups;
using Sms.Application.Numbering;
using Sms.Application.Seeding;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Common;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Seeding;

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

            services.AddScoped<INumberingSeriesAdmin, NumberingSeriesAdmin>();
            services.AddScoped<ILookupAdmin, LookupAdmin>();

            services.AddScoped<ISeedContributor, LookupProductSeedContributor>();
            services.AddScoped<ISeedContributor, RoleTemplateSeedContributor>();
            services.AddScoped<ISeedContributor, NumberingCatalogSeedContributor>();
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
