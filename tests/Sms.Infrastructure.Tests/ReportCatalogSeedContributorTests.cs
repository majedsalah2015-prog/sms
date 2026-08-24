using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Reports;
using Sms.Domain.Schools;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Reports;
using Sms.Infrastructure.Seeding;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// The report catalogue is content, so it is tested as content: not "did rows
    /// land" but "is every row gated by a permission that exists, runnable from the
    /// parameter bar the runner actually draws, and readable in both languages".
    /// A definition bound to the wrong gate is the failure worth catching — it is
    /// invisible in a row count and it decides who may run the report.
    /// </summary>
    public sealed class ReportCatalogSeedContributorTests : IDisposable
    {
        private sealed class Tenant : ITenantContext
        {
            public int SchoolId => 1;
        }

        private sealed class User : ICurrentUser
        {
            public int UserId => 0;
        }

        private sealed class Clock : IClock
        {
            public DateTime UtcNow => new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>The keys the runner's standard parameter bar draws a control for.</summary>
        private static readonly HashSet<string> BarKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "academicYearId", "gradeLevelId", "sectionId", "dateFrom", "dateTo",
        };

        private readonly SqliteConnection _connection;

        public ReportCatalogSeedContributorTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            db.Schools.Add(new School { NameAr = "مدرسة", NameEn = "School", LicenseNumber = "LIC-1", MinistryCode = "MIN-1" });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
            => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new Tenant(), new User(), new Clock());

        private async Task SeedAsync()
        {
            using var db = CreateContext();
            await new RoleTemplateSeedContributor(db).SeedAsync();
            await new PermissionSeedContributor(db).SeedAsync();
            await new ReportCatalogSeedContributor(db, new ReportAdmin(db, new Clock())).SeedAsync();
        }

        [Fact]
        [BusinessRule("BR-RPT-001")]
        public async Task The_three_operating_loops_land_once_and_re_running_adds_none()
        {
            await SeedAsync();
            using var db = CreateContext();
            var first = await db.ReportDefinitions.CountAsync();

            // Every row in the contributor's catalogue names a screen that exists, so
            // none is skipped for want of a gate. If this drops, a screen was renamed.
            Assert.Equal(71, first);

            await SeedAsync();
            using var again = CreateContext();
            Assert.Equal(first, await again.ReportDefinitions.CountAsync());
        }

        [Fact]
        [BusinessRule("BR-RPT-002")]
        public async Task Every_definition_is_gated_by_a_view_permission_that_exists()
        {
            await SeedAsync();
            using var db = CreateContext();

            var permissions = await db.Permissions.AsNoTracking().ToDictionaryAsync(p => p.Id);
            foreach (var definition in await db.ReportDefinitions.AsNoTracking().ToListAsync())
            {
                Assert.True(permissions.ContainsKey(definition.PermissionId), $"{definition.Code} names permission {definition.PermissionId}, which does not exist");
                var gate = permissions[definition.PermissionId];
                Assert.Equal(ActionVerb.View, gate.Action);
                Assert.Equal(definition.OwningModuleCode, gate.ModuleCode);
            }
        }

        [Fact]
        public async Task Every_definition_carries_both_languages_and_a_unique_code()
        {
            await SeedAsync();
            using var db = CreateContext();
            var definitions = await db.ReportDefinitions.AsNoTracking().ToListAsync();

            Assert.Equal(definitions.Count, definitions.Select(d => d.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var definition in definitions)
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.TitleAr), $"{definition.Code} has no Arabic title");
                Assert.False(string.IsNullOrWhiteSpace(definition.TitleEn), $"{definition.Code} has no English title");
                Assert.NotEqual(definition.TitleAr, definition.TitleEn);
            }
        }

        /// <summary>
        /// doc §9 refuses a run that is missing a required key, so a required key with
        /// no control behind it would make the report unrunnable from its own screen.
        /// studentId is the stated exception: the bar has no student picker yet and the
        /// operator supplies it through the ad-hoc parameter row.
        /// </summary>
        [Fact]
        public async Task Every_required_parameter_has_a_control_on_the_runner_bar()
        {
            await SeedAsync();
            using var db = CreateContext();

            foreach (var definition in await db.ReportDefinitions.AsNoTracking().ToListAsync())
            {
                foreach (var key in (definition.RequiredParameterKeysCsv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = key.Trim();
                    Assert.True(
                        BarKeys.Contains(trimmed) || string.Equals(trimmed, "studentId", StringComparison.OrdinalIgnoreCase),
                        $"{definition.Code} requires '{trimmed}', which the runner's parameter bar does not offer");
                }
            }
        }

        [Fact]
        [BusinessRule("BR-RPT-003")]
        public async Task The_catalogue_carries_the_sensitivity_the_doc_gives_each_report()
        {
            await SeedAsync();
            using var db = CreateContext();

            // 🔒 in docs/Reports/Report-Catalog.md — export is separately gated and
            // email delivery is refused outright.
            Assert.Equal(ReportSensitivity.Restricted, await SensitivityOf(db, "RPT-DIS-001"));
            Assert.Equal(ReportSensitivity.Restricted, await SensitivityOf(db, "RPT-STU-005"));
            Assert.Equal(ReportSensitivity.Restricted, await SensitivityOf(db, "RPT-PAY-004"));

            // PD — personal data.
            Assert.Equal(ReportSensitivity.PersonalData, await SensitivityOf(db, "RPT-FEE-004"));
            Assert.Equal(ReportSensitivity.PersonalData, await SensitivityOf(db, "RPT-ATD-001"));

            // Std.
            Assert.Equal(ReportSensitivity.Normal, await SensitivityOf(db, "RPT-PAY-001"));
        }

        /// <summary>
        /// The official-document rows are the ones that will need the PDF engine (O6),
        /// and nothing else claims a format it will never be asked for.
        /// </summary>
        [Fact]
        public async Task Only_official_documents_offer_pdf()
        {
            await SeedAsync();
            using var db = CreateContext();
            var documents = await db.ReportDefinitions.AsNoTracking()
                .Where(d => (d.SupportedFormats & OutputFormat.Pdf) != 0)
                .Select(d => d.Code)
                .ToListAsync();

            Assert.Equal(
                new[]
                {
                    "RPT-ATD-002", "RPT-ATD-008", "RPT-FEE-001", "RPT-GRA-004", "RPT-GRA-010",
                    "RPT-GRA-011", "RPT-GRA-012", "RPT-PAY-010", "RPT-STU-001",
                },
                documents.OrderBy(c => c, StringComparer.Ordinal).ToArray());

            foreach (var definition in await db.ReportDefinitions.AsNoTracking().ToListAsync())
            {
                Assert.True((definition.SupportedFormats & OutputFormat.Html) != 0, $"{definition.Code} cannot be read on screen");
            }
        }

        /// <summary>
        /// A school that retires a report owns that decision — re-seeding must not
        /// hand it back. This is the property most easily lost and least likely to be
        /// noticed, because the row count stays right either way.
        /// </summary>
        [Fact]
        public async Task A_retired_report_stays_retired_across_a_re_run()
        {
            await SeedAsync();

            using (var db = CreateContext())
            {
                var definition = await db.ReportDefinitions.SingleAsync(d => d.Code == "RPT-PAY-003");
                definition.IsActive = false;
                await db.SaveChangesAsync();
            }

            await SeedAsync();

            using var after = CreateContext();
            var retired = await after.ReportDefinitions.IgnoreQueryFilters().SingleAsync(d => d.Code == "RPT-PAY-003");
            Assert.False(retired.IsActive);
            Assert.Single(await after.ReportDefinitions.IgnoreQueryFilters().Where(d => d.Code == "RPT-PAY-003").ToListAsync());
        }

        private static async Task<ReportSensitivity> SensitivityOf(AppDbContext db, string code)
            => (await db.ReportDefinitions.AsNoTracking().SingleAsync(d => d.Code == code)).Sensitivity;
    }
}
