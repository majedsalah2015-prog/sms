using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Numbering;
using Sms.Application.Seeding;
using Sms.Domain.Lookups;
using Sms.Domain.Numbering;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Seeding;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-010: the lookup framework (BR-SET-001/002/007) and the demo-tenant
    /// seeder harness over a real Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class LookupAndSeedingTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public LookupAndSeedingTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        // --- ILookupAdmin (BR-SET-001/002) ------------------------------------

        [Fact]
        [BusinessRule("BR-SET-001")]
        public async Task Defining_a_category_then_values_creates_both_with_the_right_tier()
        {
            using var db = CreateContext();
            var admin = new LookupAdmin(db);

            await admin.DefineCategoryAsync("BloodType", LookupCategoryTier.ProductSeeded, "فصيلة الدم", "Blood Type");
            await admin.DefineValueAsync("BloodType", "O+", "O+", "O+", sortOrder: 1);

            var category = db.LookupCategories.Single(c => c.Code == "BloodType");
            Assert.Equal(LookupCategoryTier.ProductSeeded, category.Tier);
            var value = db.LookupValues.Single(v => v.Code == "O+");
            Assert.Equal(category.Id, value.LookupCategoryId);
            Assert.Equal("O+", value.Name.NameEn);
        }

        [Fact]
        [BusinessRule("BR-SET-001")]
        public async Task Redefining_a_category_upserts_rather_than_duplicating()
        {
            using var db = CreateContext();
            var admin = new LookupAdmin(db);

            await admin.DefineCategoryAsync("IdType", LookupCategoryTier.ProductSeeded, "نوع الهوية", "ID Type");
            await admin.DefineCategoryAsync("IdType", LookupCategoryTier.ProductSeeded, "نوع الهوية", "Identity Type");

            Assert.Single(db.LookupCategories.Where(c => c.Code == "IdType"));
            Assert.Equal("Identity Type", db.LookupCategories.Single(c => c.Code == "IdType").Name.NameEn);
        }

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task Deactivating_a_value_never_physically_removes_it()
        {
            using var db = CreateContext();
            var admin = new LookupAdmin(db);
            await admin.DefineCategoryAsync("IdType", LookupCategoryTier.ProductSeeded, "نوع الهوية", "ID Type");
            var value = await admin.DefineValueAsync("IdType", "Passport", "جواز سفر", "Passport", sortOrder: 1);

            await admin.DeactivateValueAsync(value.Id);

            var stored = db.LookupValues.IgnoreQueryFilters().Single(v => v.Id == value.Id);
            Assert.False(stored.IsActive);
        }

        // --- SeedRunner + contributors -----------------------------------------

        [Fact]
        public async Task SeedRunner_runs_every_contributor_in_order()
        {
            var order = new List<string>();
            var first = new RecordingContributor("A", 2, order);
            var second = new RecordingContributor("B", 1, order);
            var runner = new SeedRunner(new ISeedContributor[] { first, second });

            var ran = await runner.RunAllAsync();

            Assert.Equal(new[] { "B", "A" }, order);
            Assert.Equal(new[] { "B", "A" }, ran);
        }

        [Fact]
        [BusinessRule("BR-SET-001")]
        public async Task Lookup_product_seed_contributor_is_idempotent()
        {
            using var db = CreateContext();
            var contributor = new LookupProductSeedContributor(new LookupAdmin(db));

            await contributor.SeedAsync();
            var firstCount = db.LookupValues.Count();
            await contributor.SeedAsync();
            var secondCount = db.LookupValues.Count();

            Assert.Equal(firstCount, secondCount);
            Assert.True(firstCount > 0);
            Assert.Contains(db.LookupValues, v => v.Code == "SA");
        }

        [Fact]
        public async Task Role_template_seed_contributor_seeds_all_21_templates_idempotently()
        {
            using var db = CreateContext();
            var contributor = new RoleTemplateSeedContributor(db);

            await contributor.SeedAsync();
            await contributor.SeedAsync();

            Assert.Equal(21, db.Roles.Count());
            var sysAdmin = db.Roles.Single(r => r.Code == "SYSADMIN");
            Assert.True(sysAdmin.RequireTwoFactor);
            var teacher = db.Roles.Single(r => r.Code == "TEACHER");
            Assert.False(teacher.RequireTwoFactor);
        }

        [Fact]
        [BusinessRule("BR-NUM-001")]
        public async Task Numbering_catalog_seed_contributor_seeds_the_full_doc08_catalog_idempotently()
        {
            using var db = CreateContext();
            var admin = new NumberingSeriesAdmin(db);
            var contributor = new NumberingCatalogSeedContributor(admin, _clock);

            await contributor.SeedAsync();
            var firstVersionCount = db.NumberingSeries.Count();
            await contributor.SeedAsync();

            Assert.Equal(19, db.NumberingSeries.Count(s => s.IsActive));
            Assert.Equal(firstVersionCount, db.NumberingSeries.Count()); // no spurious cutover on re-seed
            var student = db.NumberingSeries.Single(s => s.Code == "STU");
            Assert.Equal(ResetPolicy.Never, student.ResetPolicy);
            var receipt = db.NumberingSeries.Single(s => s.Code == "RCP");
            Assert.Equal(GapPolicy.Strict, receipt.GapPolicy);
        }

        private sealed class RecordingContributor : ISeedContributor
        {
            private readonly List<string> _order;

            public RecordingContributor(string name, int order, List<string> sink)
            {
                Name = name;
                Order = order;
                _order = sink;
            }

            public string Name { get; }

            public int Order { get; }

            public Task SeedAsync(CancellationToken cancellationToken = default)
            {
                _order.Add(Name);
                return Task.CompletedTask;
            }
        }
    }
}
