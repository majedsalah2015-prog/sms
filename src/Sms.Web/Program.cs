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
using ERP2028.Modules.Cash.Infrastructure.Persistence;
using ERP2028.Modules.Cash.Infrastructure.Seeding;
using ERP2028.Modules.Inventory.Application.Labels.Design;
using ERP2028.Modules.Inventory.Domain.Repositories;
using ERP2028.Modules.Inventory.Infrastructure.Persistence;
using ERP2028.Modules.Inventory.Infrastructure.Seeding;
using ERP2028.Modules.Organization.Infrastructure.Persistence;
using ERP2028.Modules.Organization.Infrastructure.Seeding;
using ERP2028.Modules.Partners.Infrastructure.Persistence;
using ERP2028.Modules.Partners.Infrastructure.Seeding;
using ERP2028.Modules.Purchasing.Infrastructure.Persistence;
using ERP2028.Modules.Purchasing.Infrastructure.Seeding;
using ERP2028.Modules.Sales.Infrastructure.Persistence;
using ERP2028.Modules.Sales.Infrastructure.Seeding;

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

            // The ERP's modules first, in the order their contracts point in — the same order its own
            // host migrates them. Organization leads because every BranchCode written elsewhere
            // validates against org.Branches, and Accounting follows because nothing can post to a
            // ledger that does not exist yet. Each module keeps its own __EFMigrationsHistory inside
            // its own schema, so these eight migration streams share one database without ever
            // meeting.
            Migrate(services.GetRequiredService<OrganizationDbContext>(), "Organization (org)", logger);
            Migrate(services.GetRequiredService<AccountingDbContext>(), "Accounting (acc)", logger);
            Migrate(services.GetRequiredService<InventoryDbContext>(), "Inventory (inv)", logger);
            Migrate(services.GetRequiredService<PurchasingDbContext>(), "Purchasing (pur)", logger);
            Migrate(services.GetRequiredService<SalesDbContext>(), "Sales (sal)", logger);
            Migrate(services.GetRequiredService<CashDbContext>(), "Cash (cash)", logger);
            Migrate(services.GetRequiredService<PartnersDbContext>(), "Partners (ptn)", logger);
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
        /// Runs the embedded ERP modules' seeders. Every one is idempotent and every
        /// one is a no-op once its tables hold anything, so this is safe on every start.
        /// <para>
        /// Organization first, matching the ERP's own host: the company and its
        /// head-office branch are what a fiscal year and a chart of accounts get
        /// posted against. Accounting then brings its default Arabic chart, a
        /// fiscal year for the current calendar year, and the base currency —
        /// which is what makes the ledger postable on a fresh database rather
        /// than after a manual setup pass. The operational modules follow in the
        /// same order they migrate in, each seeding its own account mapping — the
        /// rows that say which account a stock movement or a receipt lands on, and
        /// without which the first document a user posts fails at the ledger.
        /// </para>
        /// </summary>
        private static void SeedEmbeddedAccounting(IServiceProvider services, ILogger logger)
        {
            logger.LogInformation("Seeding Organization data...");
            services.GetRequiredService<OrganizationDataSeeder>().SeedAsync().GetAwaiter().GetResult();

            logger.LogInformation("Seeding Accounting data...");
            var accountingOptions = services.GetRequiredService<IOptions<AccountingSeedOptions>>().Value;
            services.GetRequiredService<AccountingDataSeeder>().SeedAsync(accountingOptions).GetAwaiter().GetResult();

            logger.LogInformation("Seeding Inventory data...");
            var inventoryOptions = services.GetRequiredService<IOptions<InventorySeedOptions>>().Value;
            services.GetRequiredService<InventoryDataSeeder>().SeedAsync(inventoryOptions).GetAwaiter().GetResult();

            // The item master's built-in reference data — item types, units, barcode types. Separate
            // from the seeder above because it takes no options: none of it is a deployment choice.
            logger.LogInformation("Seeding Item Master lookups...");
            services.GetRequiredService<ItemMasterLookupSeeder>().SeedAsync().GetAwaiter().GetResult();

            // The demo weighted-barcode formats, all seeded DISABLED: an enabled one would change the
            // meaning of every 13-digit barcode starting 21 on the day it ran.
            logger.LogInformation("Seeding barcode scale configurations...");
            services.GetRequiredService<BarcodeScaleSeeder>().SeedAsync().GetAwaiter().GetResult();

            SeedLabelDesigner(services, logger);

            logger.LogInformation("Seeding Purchasing data...");
            var purchasingOptions = services.GetRequiredService<IOptions<PurchasingSeedOptions>>().Value;
            services.GetRequiredService<PurchasingDataSeeder>().SeedAsync(purchasingOptions).GetAwaiter().GetResult();

            logger.LogInformation("Seeding Sales data...");
            var salesOptions = services.GetRequiredService<IOptions<SalesSeedOptions>>().Value;
            services.GetRequiredService<SalesDataSeeder>().SeedAsync(salesOptions).GetAwaiter().GetResult();

            logger.LogInformation("Seeding Cash data...");
            var cashOptions = services.GetRequiredService<IOptions<CashSeedOptions>>().Value;
            services.GetRequiredService<CashDataSeeder>().SeedAsync(cashOptions).GetAwaiter().GetResult();

            // Partners seeds only its account mapping — who owns the school is not something a
            // default chart can guess, so no sample partner is created.
            logger.LogInformation("Seeding Partners data...");
            var partnersOptions = services.GetRequiredService<IOptions<PartnersSeedOptions>>().Value;
            services.GetRequiredService<PartnersDataSeeder>().SeedAsync(partnersOptions).GetAwaiter().GetResult();

            logger.LogInformation("Embedded accounting seed complete.");
        }

        /// <summary>
        /// The label designer's field catalogue and starter templates. Publishing is the application
        /// layer's act rather than the seeder's, and it is done here, once, on the boot that seeded
        /// them: a template with no published version cannot be printed, and a starter set nobody can
        /// print is an empty designer with extra steps. Later boots skip it entirely, so a user's
        /// half-edited draft is never auto-published.
        /// </summary>
        private static void SeedLabelDesigner(IServiceProvider services, ILogger logger)
        {
            logger.LogInformation("Seeding label designer catalogue...");
            var seeded = services.GetRequiredService<LabelDesignerSeeder>().SeedAsync().GetAwaiter().GetResult();
            if (!seeded)
            {
                return;
            }

            var templates = services.GetRequiredService<ILabelTemplateRepository>();
            var templateService = services.GetRequiredService<ILabelTemplateService>();
            foreach (var starterCode in LabelDesignerSeeder.StarterTemplateCodes)
            {
                var starter = templates.GetByCodeAsync(starterCode).GetAwaiter().GetResult();
                if (starter is null || starter.IsPublished)
                {
                    continue;
                }

                var published = templateService.PublishAsync(starter.Id, "Seeded starter template")
                    .GetAwaiter().GetResult();
                if (published.IsFailure)
                {
                    logger.LogWarning(
                        "Starter label template {Code} could not be published: {Error}",
                        starterCode, string.Join("; ", published.Errors.Select(e => e.Message)));
                }
            }
        }
    }
}
