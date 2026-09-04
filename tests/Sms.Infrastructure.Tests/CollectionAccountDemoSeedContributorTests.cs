using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Seeding;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// doc/Modules/21 §3 BR-PAY-002 — the demo tenant's collection accounts,
    /// and the reason they are not seeded by <c>DemoSeedContributor</c>.
    /// <para>
    /// The regression these cover is a silent one: the accounts sat at the tail
    /// of a method gated on "does any school exist yet", so on every database
    /// provisioned before the catalogue was built they were never written, the
    /// seeder reported success, and the cashier's destination picker offered
    /// nothing. Each test here starts from a school that already exists — the
    /// state the old placement could not reach.
    /// </para>
    /// </summary>
    public sealed class CollectionAccountDemoSeedContributorTests : IDisposable
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

        public CollectionAccountDemoSeedContributorTests()
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

        private static CollectionAccountDemoSeedContributor Contributor(AppDbContext db)
            => new(db, new CollectionAccountAdmin(db));

        private static async Task AddSchoolAsync(AppDbContext db)
        {
            db.Schools.Add(new School { NameAr = "مدرسة", NameEn = "School", LicenseNumber = "LIC-1", MinistryCode = "MIN-1" });
            await db.SaveChangesAsync();
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task Seeds_both_kinds_into_a_school_that_already_exists()
        {
            using var db = CreateContext();
            await AddSchoolAsync(db);

            await Contributor(db).SeedAsync();

            var accounts = await db.CollectionAccounts.AsNoTracking().ToListAsync();
            Assert.Equal(2, accounts.Count);

            // One of each kind, or CollectionAccountSelector.KindFor leaves half the payment
            // methods with nothing to point at: cash needs a cash box, transfer/card/cheque a bank.
            var cashBox = Assert.Single(accounts.Where(a => a.Kind == CollectionAccountKind.CashBox));
            var bank = Assert.Single(accounts.Where(a => a.Kind == CollectionAccountKind.Bank));
            Assert.Equal("SAFE-MAIN", cashBox.Code);
            Assert.Equal("BANK-MAIN", bank.Code);
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task The_bank_account_carries_what_a_parent_is_read_out()
        {
            using var db = CreateContext();
            await AddSchoolAsync(db);

            await Contributor(db).SeedAsync();

            // The picker exists to answer "where do I send the transfer" — an account with no
            // number answers nothing, and CollectionAccountAdmin refuses one anyway.
            var bank = await db.CollectionAccounts.AsNoTracking().SingleAsync(a => a.Kind == CollectionAccountKind.Bank);
            Assert.False(string.IsNullOrWhiteSpace(bank.Iban));
            Assert.False(string.IsNullOrWhiteSpace(bank.AccountNo));
            Assert.False(string.IsNullOrWhiteSpace(bank.BankName));
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task Both_are_pre_selected_because_the_default_is_per_kind()
        {
            using var db = CreateContext();
            await AddSchoolAsync(db);

            await Contributor(db).SeedAsync();

            // ApplyDefaultAsync clears other defaults of the same kind only. Two kinds, two
            // defaults — were that ever made global, one of the two methods would open unset.
            var accounts = await db.CollectionAccounts.AsNoTracking().ToListAsync();
            Assert.All(accounts, a => Assert.True(a.IsDefault));
        }

        [Fact]
        [BusinessRule("BR-PAY-002")]
        public async Task A_default_the_school_already_chose_is_not_taken_over()
        {
            using var db = CreateContext();
            await AddSchoolAsync(db);
            await new CollectionAccountAdmin(db).DefineAsync(
                "BANK-01", "حساب المدرسة", "School account", CollectionAccountKind.Bank,
                bankName: "بنك", iban: "SA9999999999999999999999", isDefault: true);

            await Contributor(db).SeedAsync();

            // DefineAsync clears the other default of its kind, so seeding one unconditionally
            // would re-point the cashier's pre-selection at demo data on every run.
            var school = await db.CollectionAccounts.AsNoTracking().SingleAsync(a => a.Code == "BANK-01");
            var seeded = await db.CollectionAccounts.AsNoTracking().SingleAsync(a => a.Code == "BANK-MAIN");
            Assert.True(school.IsDefault);
            Assert.False(seeded.IsDefault);

            // The cash box had no default to defend, so that one still gets pre-selected.
            var cashBox = await db.CollectionAccounts.AsNoTracking().SingleAsync(a => a.Code == "SAFE-MAIN");
            Assert.True(cashBox.IsDefault);
        }

        [Fact]
        public async Task Running_twice_adds_nothing()
        {
            using var db = CreateContext();
            await AddSchoolAsync(db);

            await Contributor(db).SeedAsync();
            await Contributor(db).SeedAsync();

            Assert.Equal(2, await db.CollectionAccounts.CountAsync());
        }

        [Fact]
        public async Task A_code_the_school_already_uses_is_left_alone_rather_than_refused()
        {
            using var db = CreateContext();
            await AddSchoolAsync(db);
            await new CollectionAccountAdmin(db).DefineAsync(
                "BANK-MAIN", "حساب المدرسة", "School account", CollectionAccountKind.Bank,
                bankName: "بنك آخر", iban: "SA9999999999999999999999");

            // Not an exception: the seeder runs on every deployment, and a school that named its
            // own account BANK-MAIN must not have the whole run die on a duplicate-code refusal.
            await Contributor(db).SeedAsync();

            var bank = await db.CollectionAccounts.AsNoTracking().SingleAsync(a => a.Kind == CollectionAccountKind.Bank);
            Assert.Equal("School account", bank.NameEn);
            Assert.Equal(2, await db.CollectionAccounts.CountAsync());
        }

        [Fact]
        public async Task A_retired_account_is_not_resurrected()
        {
            using var db = CreateContext();
            await AddSchoolAsync(db);
            await Contributor(db).SeedAsync();
            var bankId = (await db.CollectionAccounts.SingleAsync(a => a.Kind == CollectionAccountKind.Bank)).Id;
            await new CollectionAccountAdmin(db).DeactivateAsync(bankId);

            await Contributor(db).SeedAsync();

            // A school that closed the account meant to. Matching on the unfiltered set is what
            // keeps the next seeder run from quietly standing a second one up beside it.
            Assert.Equal(2, await db.CollectionAccounts.IgnoreQueryFilters().CountAsync());
            var bank = await db.CollectionAccounts.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == bankId);
            Assert.False(bank.IsActive);
        }

        [Fact]
        public async Task Writes_nothing_before_a_school_exists()
        {
            using var db = CreateContext();

            await Contributor(db).SeedAsync();

            // The silent no-op SeedOrderTests guards the ordering against — asserted here so the
            // guard is known to be guarding something real.
            Assert.Empty(await db.CollectionAccounts.IgnoreQueryFilters().ToListAsync());
        }
    }
}
