using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Numbering;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-006 numbering framework over a real Sqlite-backed AppDbContext —
    /// including a deterministic interleaving of two contexts to prove the
    /// BR-NUM-003 concurrency-token mechanism without relying on real thread
    /// races (which fight Sqlite's own connection-locking, not the thing
    /// under test).
    /// </summary>
    public sealed class NumberingTests : IDisposable
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

        public NumberingTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private static NumberingSeriesAdmin Admin(AppDbContext db) => new(db);

        private static NumberIssuer Issuer(AppDbContext db, FixedTenant tenant, FixedClock clock) => new(db, tenant, tenant, clock);

        // --- INumberingSeriesAdmin: definition + cutover (BR-NUM-005) --------

        [Fact]
        [BusinessRule("BR-NUM-005")]
        public async Task Defining_a_new_code_creates_version_one_unlocked()
        {
            using var db = CreateContext();
            var series = await Admin(db).DefineSeriesAsync(
                "STU", "Student", "STU-{YEAR}-{SEQ:5}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow);

            Assert.Equal(1, series.Version);
            Assert.True(series.IsActive);
            Assert.False(series.IsLocked);
        }

        [Fact]
        [BusinessRule("BR-NUM-005")]
        public async Task An_unlocked_series_is_edited_in_place_not_versioned()
        {
            using var db = CreateContext();
            var admin = Admin(db);
            var first = await admin.DefineSeriesAsync("STU", "Student", "STU-{SEQ:5}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow);

            _audit.Reason = "Fixing the initial format before go-live";
            var edited = await admin.DefineSeriesAsync("STU", "Student", "STU-{YEAR}-{SEQ:5}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow);

            Assert.Equal(first.Id, edited.Id);
            Assert.Equal(1, edited.Version);
            Assert.Equal("STU-{YEAR}-{SEQ:5}", db.NumberingSeries.Single(s => s.Id == first.Id).FormatTemplate);
        }

        [Fact]
        [BusinessRule("BR-NUM-005")]
        public async Task Redefining_a_locked_series_deactivates_it_and_opens_version_two()
        {
            using var db = CreateContext();
            var admin = Admin(db);
            var v1 = await admin.DefineSeriesAsync("RCP", "Receipt", "RCP-{SEQ:6}", ResetPolicy.PerCalendarYear, GapPolicy.Strict, _clock.UtcNow);
            await Issuer(db, _tenant, _clock).IssueAsync("RCP"); // locks it
            await db.SaveChangesAsync();

            _audit.Reason = "Switching to the school-code'd format for the new fiscal year";
            var v2 = await admin.DefineSeriesAsync("RCP", "Receipt", "RCP/{SCHOOL}/{SEQ:6}", ResetPolicy.PerCalendarYear, GapPolicy.Strict, _clock.UtcNow.AddDays(1));

            Assert.NotEqual(v1.Id, v2.Id);
            Assert.Equal(2, v2.Version);
            Assert.False(db.NumberingSeries.Single(s => s.Id == v1.Id).IsActive);
            Assert.True(v2.IsActive);

            // The old, deactivated version stays queryable for continuity reporting (doc 08 §7).
            Assert.NotNull(db.NumberingSeries.SingleOrDefault(s => s.Id == v1.Id));
        }

        [Fact]
        [BusinessRule("BR-NUM-005")]
        public async Task Redefining_a_locked_series_with_the_same_definition_is_a_no_op()
        {
            using var db = CreateContext();
            var admin = Admin(db);
            var v1 = await admin.DefineSeriesAsync("PAR", "Parent", "PAR-{SEQ:6}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow);
            await Issuer(db, _tenant, _clock).IssueAsync("PAR");
            await db.SaveChangesAsync();

            // An idempotent seed re-run must not open a new version (that restarted numbering in production).
            var again = await admin.DefineSeriesAsync("PAR", "Parent", "PAR-{SEQ:6}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow.AddDays(3));
            Assert.Equal(v1.Id, again.Id);
            Assert.Equal(1, db.NumberingSeries.Count(s => s.Code == "PAR"));
        }

        [Fact]
        [BusinessRule("BR-NUM-002")]
        public async Task A_new_version_continues_the_previous_counter_instead_of_reissuing_numbers()
        {
            using var db = CreateContext();
            var admin = Admin(db);
            var issuer = Issuer(db, _tenant, _clock);
            await admin.DefineSeriesAsync("EMP", "Employee", "EMP-{SEQ:5}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow);
            var n1 = await issuer.IssueAsync("EMP");
            var n2 = await issuer.IssueAsync("EMP");
            await db.SaveChangesAsync();
            Assert.Equal("EMP-00002", n2);

            _audit.Reason = "new format";
            await admin.DefineSeriesAsync("EMP", "Employee", "E-{SEQ:5}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow.AddDays(1));
            var n3 = await issuer.IssueAsync("EMP");
            await db.SaveChangesAsync();

            Assert.Equal("E-00003", n3);
            Assert.NotEqual(n1, n3);
        }

        // --- INumberIssuer: rendering + locking (BR-NUM-001, BR-NUM-007) -----

        [Fact]
        [BusinessRule("BR-NUM-001")]
        public async Task Issuing_against_an_undefined_code_is_refused()
        {
            using var db = CreateContext();

            await Assert.ThrowsAsync<NoActiveNumberingSeriesException>(() => Issuer(db, _tenant, _clock).IssueAsync("GHOST"));
        }

        [Fact]
        [BusinessRule("BR-NUM-001")]
        public async Task Consecutive_issuances_are_sequential_and_the_first_one_locks_the_series()
        {
            using var db = CreateContext();
            var series = await Admin(db).DefineSeriesAsync("STU", "Student", "STU-{SEQ:5}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow);
            var issuer = Issuer(db, _tenant, _clock);

            var first = await issuer.IssueAsync("STU");
            await db.SaveChangesAsync();
            Assert.True(db.NumberingSeries.Single(s => s.Id == series.Id).IsLocked);

            var second = await issuer.IssueAsync("STU");
            await db.SaveChangesAsync();

            Assert.Equal("STU-00001", first);
            Assert.Equal("STU-00002", second);
        }

        [Fact]
        [BusinessRule("BR-NUM-004")]
        public async Task A_never_reset_series_keeps_counting_across_calendar_years()
        {
            using var db = CreateContext();
            await Admin(db).DefineSeriesAsync("STU", "Student", "STU-{SEQ:3}", ResetPolicy.Never, GapPolicy.Normal, _clock.UtcNow);
            var issuer = Issuer(db, _tenant, _clock);

            var first = await issuer.IssueAsync("STU");
            await db.SaveChangesAsync();

            _clock.UtcNow = _clock.UtcNow.AddYears(1);
            var second = await issuer.IssueAsync("STU");
            await db.SaveChangesAsync();

            Assert.Equal("STU-001", first);
            Assert.Equal("STU-002", second); // no reset despite the year rolling over
        }

        [Fact]
        [BusinessRule("BR-NUM-005")]
        public async Task A_per_calendar_year_series_restarts_its_sequence_on_the_next_year()
        {
            using var db = CreateContext();
            await Admin(db).DefineSeriesAsync("RCP", "Receipt", "RCP-{GYEAR}-{SEQ:3}", ResetPolicy.PerCalendarYear, GapPolicy.Strict, _clock.UtcNow);
            var issuer = Issuer(db, _tenant, _clock);

            var last2026 = await issuer.IssueAsync("RCP");
            await db.SaveChangesAsync();

            _clock.UtcNow = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            var first2027 = await issuer.IssueAsync("RCP");
            await db.SaveChangesAsync();

            Assert.Equal("RCP-2026-001", last2026);
            Assert.Equal("RCP-2027-001", first2027);
        }

        // --- BR-NUM-003: gap-free under concurrency ---------------------------

        [Fact]
        [BusinessRule("BR-NUM-003")]
        public async Task Racing_the_very_first_issuance_is_caught_by_the_series_state_unique_index()
        {
            using var setup = CreateContext();
            await Admin(setup).DefineSeriesAsync("RCP", "Receipt", "RCP-{SEQ:4}", ResetPolicy.Never, GapPolicy.Strict, _clock.UtcNow);
            await setup.SaveChangesAsync();

            // Sequential awaits are enough to simulate the race: neither context has
            // saved yet when the second one reads — both find no SeriesState row yet
            // and both try to insert one for the same (series, resetKey).
            using var ctx1 = CreateContext();
            using var ctx2 = CreateContext();
            var number1 = await Issuer(ctx1, _tenant, _clock).IssueAsync("RCP");
            var number2 = await Issuer(ctx2, _tenant, _clock).IssueAsync("RCP");
            Assert.Equal("RCP-0001", number1);
            Assert.Equal("RCP-0001", number2);

            await ctx1.SaveChangesAsync();
            // Same (NumberingSeriesId, ResetKey) inserted twice — the unique index
            // rejects the second whole transaction, no gap and no duplicate persisted.
            await Assert.ThrowsAsync<DbUpdateException>(() => ctx2.SaveChangesAsync());

            using var check = CreateContext();
            Assert.Equal(1, check.SeriesStates.Single().LastIssuedSequence);
        }

        [Fact]
        [BusinessRule("BR-NUM-003")]
        public async Task Racing_a_later_issuance_is_caught_by_the_concurrency_token_and_a_retry_stays_gap_free()
        {
            using var setup = CreateContext();
            await Admin(setup).DefineSeriesAsync("RCP", "Receipt", "RCP-{SEQ:4}", ResetPolicy.Never, GapPolicy.Strict, _clock.UtcNow);
            await Issuer(setup, _tenant, _clock).IssueAsync("RCP"); // seeds an existing SeriesState row (#1)
            await setup.SaveChangesAsync();

            // Two independent contexts both load the existing state (LastIssuedSequence
            // = 1) before either commits — the shape of two concurrent postings.
            using var ctx1 = CreateContext();
            using var ctx2 = CreateContext();
            var number1 = await Issuer(ctx1, _tenant, _clock).IssueAsync("RCP");
            var number2 = await Issuer(ctx2, _tenant, _clock).IssueAsync("RCP");
            Assert.Equal("RCP-0002", number1);
            Assert.Equal("RCP-0002", number2);

            await ctx1.SaveChangesAsync();
            // EF's UPDATE ... WHERE LastIssuedSequence = 1 no longer matches (it's now
            // 2) — the loser's whole transaction fails atomically, no gap, no duplicate.
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());

            using var checkAfterLoss = CreateContext();
            Assert.Equal(2, checkAfterLoss.SeriesStates.Single().LastIssuedSequence);

            // A well-behaved caller retries the whole posting on a fresh context.
            using var retry = CreateContext();
            var number3 = await Issuer(retry, _tenant, _clock).IssueAsync("RCP");
            await retry.SaveChangesAsync();

            Assert.Equal("RCP-0003", number3);
        }
    }
}
