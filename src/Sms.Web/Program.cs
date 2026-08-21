using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sms.Infrastructure.Persistence;
using ERP2028.Modules.Accounting.Infrastructure.Persistence;
using ERP2028.Modules.Accounting.Infrastructure.Seeding;
using ERP2028.Modules.Organization.Infrastructure.Persistence;
using ERP2028.Modules.Organization.Infrastructure.Seeding;

namespace Sms.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            ApplyPendingMigrations(host);
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        /// <summary>
        /// Brings the tenant database up to the current migration before the
        /// host starts (so it runs ahead of Hangfire's own schema bootstrap,
        /// which needs the database to exist). Enabled by default in
        /// Development; elsewhere it must be opted into via
        /// <c>Database:MigrateOnStartup=true</c>, because doc 02 §9 requires
        /// per-tenant upgrades to run under the M35 pre-op snapshot process,
        /// not as an unattended side effect of app start.
        /// </summary>
        private static void ApplyPendingMigrations(IHost host)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var configuration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IHostEnvironment>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            var enabled = configuration.GetValue("Database:MigrateOnStartup", environment.IsDevelopment());
            if (!enabled)
            {
                logger.LogInformation("Database:MigrateOnStartup is off; skipping schema migration.");
                return;
            }

            // The ERP's Organization and Accounting modules first, in that order: Accounting validates
            // a posting line's BranchCode against org.Branches, so the branch master must exist before
            // anything can post. Each keeps its own __EFMigrationsHistory inside its own schema, so
            // these three migration streams share one database without ever meeting.
            Migrate(services.GetRequiredService<OrganizationDbContext>(), "Organization (org)", logger);
            Migrate(services.GetRequiredService<AccountingDbContext>(), "Accounting (acc)", logger);
            Migrate(services.GetRequiredService<AppDbContext>(), "School", logger);

            SeedEmbeddedAccounting(services, logger);
        }

        private static void Migrate(DbContext db, string label, ILogger logger)
        {
            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("{Label} schema is current; no pending migrations.", label);
                return;
            }

            logger.LogInformation("{Label}: applying {Count} pending migration(s): {Migrations}", label, pending.Count, string.Join(", ", pending));
            db.Database.Migrate();
            logger.LogInformation("{Label} schema migrated.", label);
        }

        /// <summary>
        /// Runs the two ERP seeders. Both are idempotent and both are no-ops once
        /// their tables hold anything, so this is safe on every start.
        /// <para>
        /// Organization first, matching the ERP's own host: the company and its
        /// head-office branch are what a fiscal year and a chart of accounts get
        /// posted against. Accounting then brings its default Arabic chart, a
        /// fiscal year for the current calendar year, and the base currency —
        /// which is what makes the ledger postable on a fresh database rather
        /// than after a manual setup pass.
        /// </para>
        /// </summary>
        private static void SeedEmbeddedAccounting(IServiceProvider services, ILogger logger)
        {
            logger.LogInformation("Seeding Organization data...");
            services.GetRequiredService<OrganizationDataSeeder>().SeedAsync().GetAwaiter().GetResult();

            logger.LogInformation("Seeding Accounting data...");
            var accountingOptions = services.GetRequiredService<IOptions<AccountingSeedOptions>>().Value;
            services.GetRequiredService<AccountingDataSeeder>().SeedAsync(accountingOptions).GetAwaiter().GetResult();

            logger.LogInformation("Accounting seed complete.");
        }
    }
}
