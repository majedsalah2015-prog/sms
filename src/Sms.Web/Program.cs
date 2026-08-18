using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Infrastructure.Persistence;

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

            var db = services.GetRequiredService<AppDbContext>();
            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("Database schema is current; no pending migrations.");
                return;
            }

            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
            db.Database.Migrate();
            logger.LogInformation("Database schema migrated.");
        }
    }
}
