using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Domain.Parents;
using Sms.Domain.Security;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;
using Sms.Infrastructure.Seeding;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-304 follow-up: the demo portal account contributor bridges the E-305
    /// demo parent to a portal UserAccount so BR-SEC-010..013 can be exercised
    /// against a real sign-in. Same Sqlite fixture shape as the other seeding
    /// tests.
    /// </summary>
    public sealed class PortalDemoAccountSeedContributorTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc);
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
        private readonly IPasswordHasher _hasher = new PasswordHasher();

        public PortalDemoAccountSeedContributorTests()
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

        private PortalDemoAccountSeedContributor CreateContributor(AppDbContext db)
            => new(db, new AuthenticationService(db, _hasher, _clock, new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit)));

        private static Parent DemoParent() => new()
        {
            ParentFileNo = "PAR-000001",
            NameAr = "ولي الأمر",
            NameEn = "Guardian",
            PrimaryMobile = PortalDemoAccountSeedContributor.DemoParentMobile,
        };

        [Fact]
        [BusinessRule("BR-SEC-010")]
        public async Task Bridges_the_demo_parent_to_a_portal_account_with_a_temporary_password()
        {
            using var db = CreateContext();
            db.Parents.Add(DemoParent());
            await db.SaveChangesAsync();

            await CreateContributor(db).SeedAsync();

            var account = await db.UserAccounts.SingleAsync(u => u.UserName == PortalDemoAccountSeedContributor.UserName);
            Assert.Equal(AccountType.Parent, account.AccountType);
            Assert.True(account.MustChangePassword); // BR-SEC-005 one-time credential
            var parent = await db.Parents.SingleAsync(p => p.PrimaryMobile == PortalDemoAccountSeedContributor.DemoParentMobile);
            Assert.Equal(account.Id, parent.UserAccountId);
        }

        [Fact]
        public async Task Is_idempotent_on_rerun()
        {
            using var db = CreateContext();
            db.Parents.Add(DemoParent());
            await db.SaveChangesAsync();

            await CreateContributor(db).SeedAsync();
            await CreateContributor(db).SeedAsync();

            Assert.Equal(1, await db.UserAccounts.CountAsync(u => u.UserName == PortalDemoAccountSeedContributor.UserName));
        }

        [Fact]
        public async Task Does_nothing_when_the_demo_parent_is_absent()
        {
            using var db = CreateContext();

            await CreateContributor(db).SeedAsync();

            Assert.False(await db.UserAccounts.AnyAsync(u => u.UserName == PortalDemoAccountSeedContributor.UserName));
        }
    }
}
