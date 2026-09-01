using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Geography;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Geography;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// The residence constants a student's and a guardian's address are chosen from — محافظة →
    /// منطقة → حي — over a real Sqlite-backed <see cref="AppDbContext"/>.
    /// <para>
    /// The hierarchy shipped seeded and with no screen behind it, so what these cover is everything
    /// the first maintenance screen made reachable: adding a level under its parent, correcting a
    /// name without disturbing the code every address keys on, retiring a row without losing the
    /// addresses that name it, and the tenant boundary — the one failure here that would be silent.
    /// </para>
    /// </summary>
    public sealed class ResidenceAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 31, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        /// <summary>Mutable, so one test can look at the same database as another school.</summary>
        private sealed class MovableTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId { get; set; } = 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly MovableTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public ResidenceAdminTests()
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

        // --- adding -----------------------------------------------------------

        [Fact]
        public async Task A_governorate_added_without_a_code_gets_one_derived_from_its_English_name()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);

            var row = await admin.SaveGovernorateAsync(null, code: null, "غزة", "Gaza", 1);

            Assert.Equal("GAZA", row.Code);
            Assert.Equal("غزة", row.Name.NameAr);
            Assert.True(row.IsActive);
        }

        [Fact]
        public async Task A_typed_code_that_is_already_taken_at_that_level_is_refused()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);
            await admin.SaveGovernorateAsync(null, "60", "غزة", "Gaza", 1);

            var refusal = await Assert.ThrowsAsync<DuplicateResidenceCodeException>(
                () => admin.SaveGovernorateAsync(null, "60", "شمال غزة", "North Gaza", 2));

            Assert.Equal(ResidenceLevel.Governorate, refusal.Level);
            Assert.Equal("60", refusal.Code);
        }

        [Fact]
        public async Task A_generated_code_steps_past_a_collision_rather_than_refusing()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);
            await admin.SaveGovernorateAsync(null, null, "غزة", "Gaza", 1);

            var second = await admin.SaveGovernorateAsync(null, null, "غزة الجديدة", "Gaza", 2);

            Assert.Equal("GAZA2", second.Code);
        }

        [Fact]
        public async Task Two_localities_under_different_governorates_may_carry_the_same_code()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);
            var north = await admin.SaveGovernorateAsync(null, "55", "شمال غزة", "North Gaza", 1);
            var rafah = await admin.SaveGovernorateAsync(null, "75", "رفح", "Rafah", 5);

            await admin.SaveLocalityAsync(null, north.Id, "CENTRAL", "الوسط", "Central", 1);
            var second = await admin.SaveLocalityAsync(null, rafah.Id, "CENTRAL", "الوسط", "Central", 1);

            // Unique per governorate, not per school: "Central" names a different place under each,
            // and forcing globally distinct codes would push the parent's name into the child's code.
            Assert.Equal("CENTRAL", second.Code);
        }

        [Fact]
        public async Task A_quarter_is_added_under_the_locality_it_belongs_to()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);
            var gov = await admin.SaveGovernorateAsync(null, "60", "غزة", "Gaza", 1);
            var locality = await admin.SaveLocalityAsync(null, gov.Id, null, "غزة", "Gaza City", 1);

            var quarter = await admin.SaveQuarterAsync(null, locality.Id, null, "حي النصر", "An-Nasr", 1);

            Assert.Equal(locality.Id, quarter.ResidenceAreaId);
            Assert.Equal("ANNASR", quarter.Code);
        }

        // --- editing ----------------------------------------------------------

        [Fact]
        public async Task Correcting_a_name_leaves_the_code_alone()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);
            var gov = await admin.SaveGovernorateAsync(null, "65", "دير البلح", "Deir Al Balah", 3);

            var edited = await admin.SaveGovernorateAsync(gov.Id, "IGNORED", "دير البلح", "Deir Al-Balah", 3);

            // The code is the key the seeder is idempotent on. A rename that moved it would have the
            // next seed run insert the original again beside the row that was renamed.
            Assert.Equal(gov.Id, edited.Id);
            Assert.Equal("65", edited.Code);
            Assert.Equal("Deir Al-Balah", edited.Name.NameEn);
        }

        [Fact]
        public async Task Editing_a_row_that_is_no_longer_there_is_refused_with_its_level()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);

            var refusal = await Assert.ThrowsAsync<ResidenceRowNotFoundException>(
                () => admin.SaveGovernorateAsync(4040, null, "لا شيء", "Nothing", 1));

            Assert.Equal(ResidenceLevel.Governorate, refusal.Level);
            Assert.Equal(4040, refusal.Id);
        }

        [Fact]
        public async Task A_locality_cannot_be_hung_from_a_governorate_that_does_not_exist()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);

            var refusal = await Assert.ThrowsAsync<ResidenceRowNotFoundException>(
                () => admin.SaveLocalityAsync(null, 9090, null, "منطقة", "Locality", 1));

            Assert.Equal(ResidenceLevel.Governorate, refusal.Level);
        }

        // --- retiring, never deleting ----------------------------------------

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task A_deactivated_locality_is_still_there_and_can_be_put_back()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);
            var gov = await admin.SaveGovernorateAsync(null, "60", "غزة", "Gaza", 1);
            var locality = await admin.SaveLocalityAsync(null, gov.Id, null, "الرمال", "Ar-Rimal", 1);

            await admin.SetLocalityActiveAsync(locality.Id, false);

            using (var reading = CreateContext())
            {
                // Gone from the pickers — the soft-active filter is what the address drop-downs read...
                Assert.Empty(await reading.ResidenceAreas.Where(a => a.Id == locality.Id).ToListAsync());

                // ...and still a row, with its address still legible on whatever points at it.
                var retired = await reading.ResidenceAreas.IgnoreQueryFilters().SingleAsync(a => a.Id == locality.Id);
                Assert.False(retired.IsActive);
                Assert.Equal("الرمال", retired.Name.NameAr);
            }

            await admin.SetLocalityActiveAsync(locality.Id, true);

            using var after = CreateContext();
            Assert.Single(await after.ResidenceAreas.Where(a => a.Id == locality.Id).ToListAsync());
        }

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task Reactivating_reuses_the_row_rather_than_inserting_a_second_one()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);
            var gov = await admin.SaveGovernorateAsync(null, "70", "خان يونس", "Khan Yunis", 4);
            await admin.SetGovernorateActiveAsync(gov.Id, false);

            // Through the filter this row is invisible, and an "add" would have collided with the
            // unique index on (SchoolId, Code) — the failure this reads past the filter to avoid.
            await admin.SetGovernorateActiveAsync(gov.Id, true);

            using var after = CreateContext();
            var all = await after.Governorates.IgnoreQueryFilters().Where(g => g.Code == "70").ToListAsync();
            Assert.Single(all);
            Assert.True(all[0].IsActive);
        }

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task Correcting_a_retired_row_puts_it_back_into_the_lists()
        {
            using var db = CreateContext();
            var admin = new ResidenceAdmin(db);
            var gov = await admin.SaveGovernorateAsync(null, "75", "رفح", "Rafah", 5);
            await admin.SetGovernorateActiveAsync(gov.Id, false);

            // An operator who retypes the name of a retired row means to have it back; leaving it
            // retired would look like the edit was ignored.
            var edited = await admin.SaveGovernorateAsync(gov.Id, null, "رفح", "Rafah", 5);

            Assert.True(edited.IsActive);
        }

        // --- the tenant boundary ---------------------------------------------

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public async Task One_school_cannot_add_a_locality_under_another_schools_governorate()
        {
            Governorate theirs;
            using (var db = CreateContext())
            {
                theirs = await new ResidenceAdmin(db).SaveGovernorateAsync(null, "60", "غزة", "Gaza", 1);
            }

            _tenant.SchoolId = 2;
            using var mine = CreateContext();

            var refusal = await Assert.ThrowsAsync<ResidenceRowNotFoundException>(
                () => new ResidenceAdmin(mine).SaveLocalityAsync(null, theirs.Id, null, "منطقة", "Locality", 1));

            Assert.Equal(ResidenceLevel.Governorate, refusal.Level);
        }

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public async Task One_school_cannot_retire_another_schools_governorate()
        {
            Governorate theirs;
            using (var db = CreateContext())
            {
                theirs = await new ResidenceAdmin(db).SaveGovernorateAsync(null, "60", "غزة", "Gaza", 1);
            }

            _tenant.SchoolId = 2;
            using (var mine = CreateContext())
            {
                await Assert.ThrowsAsync<ResidenceRowNotFoundException>(
                    () => new ResidenceAdmin(mine).SetGovernorateActiveAsync(theirs.Id, false));
            }

            _tenant.SchoolId = 1;
            using var back = CreateContext();
            Assert.True((await back.Governorates.SingleAsync(g => g.Id == theirs.Id)).IsActive);
        }

        [Fact]
        [BusinessRule("BR-GLB-010")]
        public async Task Each_school_keeps_its_own_governorate_codes()
        {
            using (var first = CreateContext())
            {
                await new ResidenceAdmin(first).SaveGovernorateAsync(null, "60", "غزة", "Gaza", 1);
            }

            _tenant.SchoolId = 2;
            using var second = CreateContext();

            // Same code, different school: unique on (SchoolId, Code), so this is not a collision.
            var mine = await new ResidenceAdmin(second).SaveGovernorateAsync(null, "60", "غزة", "Gaza", 1);

            Assert.Equal(2, mine.SchoolId);
            Assert.Single(await second.Governorates.ToListAsync());
        }
    }
}
