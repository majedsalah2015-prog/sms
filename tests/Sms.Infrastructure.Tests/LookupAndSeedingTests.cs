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

        /// <summary>
        /// The soft-active lookup trap, inside the lookup module itself. The admin
        /// read its own rows through the active filter, so a retired value was
        /// invisible to it: reactivating one tried to insert a second row with the
        /// same (category, code) and died on the unique index, and the operator got a
        /// raw DbUpdateException that the controller's catch did not even match.
        /// The reactivate button on the lookups screen was a 500.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task Reactivating_a_retired_value_revives_the_same_row_instead_of_duplicating_it()
        {
            using var db = CreateContext();
            var admin = new LookupAdmin(db);
            await admin.DefineCategoryAsync("IdType", LookupCategoryTier.ProductSeeded, "نوع الهوية", "ID Type");
            var value = await admin.DefineValueAsync("IdType", "Passport", "جواز سفر", "Passport", sortOrder: 1);
            await admin.DeactivateValueAsync(value.Id);

            // What the screen's Reactivate button does: upsert by the same code.
            var revived = await admin.DefineValueAsync("IdType", "Passport", "جواز سفر", "Passport", sortOrder: 1);

            Assert.Equal(value.Id, revived.Id);
            Assert.True(revived.IsActive);
            Assert.Single(db.LookupValues.IgnoreQueryFilters().Where(v => v.Code == "Passport").ToList());
        }

        /// <summary>
        /// The same trap from the other side: retiring an already-retired value threw
        /// "sequence contains no elements", in English, at whoever clicked twice.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task Deactivating_an_already_retired_value_is_a_no_op_not_a_crash()
        {
            using var db = CreateContext();
            var admin = new LookupAdmin(db);
            await admin.DefineCategoryAsync("IdType", LookupCategoryTier.ProductSeeded, "نوع الهوية", "ID Type");
            var value = await admin.DefineValueAsync("IdType", "Passport", "جواز سفر", "Passport", sortOrder: 1);

            await admin.DeactivateValueAsync(value.Id);
            await admin.DeactivateValueAsync(value.Id);

            Assert.False(db.LookupValues.IgnoreQueryFilters().Single(v => v.Id == value.Id).IsActive);
        }

        /// <summary>
        /// Editing a value under a category the school has retired must still work —
        /// the category lookup had the same defect, and a retired category is exactly
        /// when someone needs to correct what is filed under it.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task A_value_under_a_retired_category_can_still_be_edited()
        {
            using var db = CreateContext();
            var admin = new LookupAdmin(db);
            var category = await admin.DefineCategoryAsync("Housing", LookupCategoryTier.SchoolManaged, "السكن", "Housing");
            await admin.DefineValueAsync("Housing", "Owned", "ملك", "Owned", sortOrder: 1);

            category.IsActive = false;
            await db.SaveChangesAsync();

            var edited = await admin.DefineValueAsync("Housing", "Owned", "ملك", "Owned outright", sortOrder: 2);

            Assert.Equal("Owned outright", edited.Name.NameEn);
            Assert.Equal(2, edited.SortOrder);
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

            // 20 from doc 08 §4, plus PAY and ADV — payroll and staff advances, the owner's
            // 2026-08-28 addition to a module the docs scope payroll out of — plus DUN, the arrears
            // notice BR-INS-008 calls a numbered formal document.
            Assert.Equal(23, db.NumberingSeries.Count(s => s.IsActive));
            Assert.Equal(firstVersionCount, db.NumberingSeries.Count()); // no spurious cutover on re-seed
            var student = db.NumberingSeries.Single(s => s.Code == "STU");
            Assert.Equal(ResetPolicy.Never, student.ResetPolicy);
            var receipt = db.NumberingSeries.Single(s => s.Code == "RCP");
            Assert.Equal(GapPolicy.Strict, receipt.GapPolicy);

            // A payroll run is a money document, so its sequence is strict for the same reason a
            // receipt's is: a hole in it is a question somebody has to answer.
            var payroll = db.NumberingSeries.Single(s => s.Code == "PAY");
            Assert.Equal(GapPolicy.Strict, payroll.GapPolicy);
            Assert.Equal(ResetPolicy.PerCalendarYear, payroll.ResetPolicy);

            // An advance is not: a withdrawn request should not oblige anyone to explain a gap.
            var advance = db.NumberingSeries.Single(s => s.Code == "ADV");
            Assert.Equal(GapPolicy.Normal, advance.GapPolicy);

            // Nor is an arrears notice — no money moves when one is issued, and an officer who
            // abandons a batch half-way should not leave a hole somebody has to account for. It
            // resets per academic year because arrears are chased within a school year.
            var notice = db.NumberingSeries.Single(s => s.Code == "DUN");
            Assert.Equal(GapPolicy.Normal, notice.GapPolicy);
            Assert.Equal(ResetPolicy.PerAcademicYear, notice.ResetPolicy);
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
