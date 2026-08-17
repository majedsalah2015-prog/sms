using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Seeding;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S3/E-305 KSA-01 content pack (BR-SET-004) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class Ksa01ContentPackSeedContributorTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);
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

        public Ksa01ContentPackSeedContributorTests()
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

        [Fact]
        [BusinessRule("BR-SET-004")]
        public async Task Seeding_creates_the_HolidayType_category_and_its_values()
        {
            using var db = CreateContext();
            var contributor = new Ksa01ContentPackSeedContributor(new LookupAdmin(db));

            await contributor.SeedAsync();

            var category = db.LookupCategories.Single(c => c.Code == "HolidayType");
            var values = db.LookupValues.Where(v => v.LookupCategoryId == category.Id).ToList();
            Assert.Equal(5, values.Count);
            Assert.Contains(values, v => v.Code == "NationalDay");
            Assert.Contains(values, v => v.Code == "EidAlFitr");
        }

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task Re_running_is_idempotent()
        {
            using var db = CreateContext();
            var contributor = new Ksa01ContentPackSeedContributor(new LookupAdmin(db));

            await contributor.SeedAsync();
            await contributor.SeedAsync();

            var category = db.LookupCategories.Single(c => c.Code == "HolidayType");
            Assert.Equal(5, db.LookupValues.Count(v => v.LookupCategoryId == category.Id));
        }
    }
}
