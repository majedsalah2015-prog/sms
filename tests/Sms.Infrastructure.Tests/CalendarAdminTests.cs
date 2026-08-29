using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Calendar;
using Sms.Domain.Schools;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Calendar;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Schools;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-103 (slice: Calendar, doc/Modules/04, BR-CAL-001/004/007) over a
    /// real Sqlite-backed AppDbContext.
    /// </summary>
    public sealed class CalendarAdminTests : IDisposable
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
        private int _yearId;

        public CalendarAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            var year = new AcademicYear
            {
                LabelAr = "٢٠٢٦-٢٠٢٧", LabelEn = "2026-2027", HijriLabel = "١٤٤٨هـ",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30),
                Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            db.SaveChanges();
            _yearId = year.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        // --- BR-CAL-001 day definition -----------------------------------------

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public async Task Defining_a_day_creates_a_manual_override()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            var day = await admin.DefineDayAsync(_yearId, new DateTime(2026, 9, 10), DayType.Holiday);

            Assert.Equal(DayType.Holiday, day.DayType);
            Assert.Equal(CalendarDaySource.Manual, day.Source);
        }

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public async Task Redefining_the_same_date_upserts_rather_than_duplicating()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            await admin.DefineDayAsync(_yearId, new DateTime(2026, 9, 10), DayType.Holiday);

            await admin.DefineDayAsync(_yearId, new DateTime(2026, 9, 10), DayType.Working);

            var stored = Assert.Single(db.CalendarDays.Where(d => d.Date == new DateTime(2026, 9, 10)));
            Assert.Equal(DayType.Working, stored.DayType);
        }

        [Fact]
        [BusinessRule("BR-GLB-051")]
        public async Task A_date_outside_the_academic_year_is_rejected()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            await Assert.ThrowsAsync<CalendarDateOutsideYearException>(() =>
                admin.DefineDayAsync(_yearId, new DateTime(2027, 8, 1), DayType.Holiday));
        }

        // --- BR-CAL-001 range painting -------------------------------------------

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public async Task Painting_a_range_writes_every_day_in_it()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            var painted = await admin.DefineDaysAsync(
                _yearId, new DateTime(2026, 9, 10), new DateTime(2026, 9, 14), DayType.Holiday);

            Assert.Equal(5, painted);
            var stored = db.CalendarDays.Where(d => d.Date >= new DateTime(2026, 9, 10) && d.Date <= new DateTime(2026, 9, 14)).ToList();
            Assert.Equal(5, stored.Count);
            Assert.All(stored, d => Assert.Equal(DayType.Holiday, d.DayType));
            Assert.All(stored, d => Assert.Equal(CalendarDaySource.Manual, d.Source));
        }

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public async Task Painting_a_range_upserts_the_days_already_defined_in_it()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            await admin.DefineDayAsync(_yearId, new DateTime(2026, 9, 12), DayType.Working);

            await admin.DefineDaysAsync(_yearId, new DateTime(2026, 9, 10), new DateTime(2026, 9, 14), DayType.Holiday);

            var stored = Assert.Single(db.CalendarDays.Where(d => d.Date == new DateTime(2026, 9, 12)));
            Assert.Equal(DayType.Holiday, stored.DayType);
            Assert.Equal(5, db.CalendarDays.Count(d => d.AcademicYearId == _yearId));
        }

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public async Task A_reversed_range_paints_the_same_days()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            var painted = await admin.DefineDaysAsync(
                _yearId, new DateTime(2026, 9, 14), new DateTime(2026, 9, 10), DayType.Holiday);

            Assert.Equal(5, painted);
        }

        [Fact]
        [BusinessRule("BR-GLB-051")]
        public async Task A_range_running_past_the_year_end_is_refused_whole()
        {
            // The board painted day by day and saved per row, so a range whose tail fell outside
            // the year left its head written and reported an error — half an amendment, and the
            // published calendar disagreeing with what the registrar thought they had done.
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            await Assert.ThrowsAsync<CalendarDateOutsideYearException>(() =>
                admin.DefineDaysAsync(_yearId, new DateTime(2027, 6, 28), new DateTime(2027, 7, 3), DayType.Holiday));

            Assert.Empty(db.CalendarDays.Where(d => d.AcademicYearId == _yearId));
        }

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public async Task A_range_starting_in_the_past_is_refused_whole()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            await Assert.ThrowsAsync<CalendarPastDateEditException>(() =>
                admin.DefineDaysAsync(_yearId, new DateTime(2026, 8, 10), new DateTime(2026, 9, 5), DayType.Holiday));

            Assert.Empty(db.CalendarDays.Where(d => d.AcademicYearId == _yearId));
        }

        // --- BR-CAL-005 provisional confirmation ---------------------------------

        [Fact]
        [BusinessRule("BR-CAL-005")]
        public async Task A_provisional_day_can_be_confirmed()
        {
            // "Flagged until confirmed" had no confirm: the flag went on and stayed on.
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var date = new DateTime(2027, 3, 20);
            await admin.DefineDayAsync(_yearId, date, DayType.Holiday, isProvisional: true);

            var confirmed = await admin.ConfirmProvisionalDayAsync(_yearId, date);

            Assert.NotNull(confirmed);
            Assert.False(confirmed!.IsProvisional);
            Assert.Equal(DayType.Holiday, confirmed.DayType);
            Assert.False(db.CalendarDays.Single(d => d.Date == date).IsProvisional);
        }

        [Fact]
        [BusinessRule("BR-CAL-005")]
        public async Task Confirming_a_day_that_is_already_confirmed_is_a_no_op()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var date = new DateTime(2027, 3, 20);
            await admin.DefineDayAsync(_yearId, date, DayType.Holiday, isProvisional: true);
            await admin.ConfirmProvisionalDayAsync(_yearId, date);

            var again = await admin.ConfirmProvisionalDayAsync(_yearId, date);

            Assert.NotNull(again);
            Assert.False(again!.IsProvisional);
        }

        [Fact]
        [BusinessRule("BR-CAL-005")]
        public async Task Confirming_a_date_the_year_has_no_row_for_returns_null()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            Assert.Null(await admin.ConfirmProvisionalDayAsync(_yearId, new DateTime(2027, 3, 20)));
        }

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public async Task A_provisional_day_that_has_passed_cannot_be_confirmed()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var date = new DateTime(2026, 9, 20);
            await admin.DefineDayAsync(_yearId, date, DayType.Holiday, isProvisional: true);
            _clock.UtcNow = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

            await Assert.ThrowsAsync<CalendarPastDateEditException>(() =>
                admin.ConfirmProvisionalDayAsync(_yearId, date));
        }

        // --- BR-CAL-004 past-date guard -----------------------------------------

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public async Task A_past_date_cannot_be_edited()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            await Assert.ThrowsAsync<CalendarPastDateEditException>(() =>
                admin.DefineDayAsync(_yearId, new DateTime(2026, 8, 1), DayType.Holiday));
        }

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public async Task An_event_starting_in_the_past_cannot_be_created()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            await Assert.ThrowsAsync<CalendarPastDateEditException>(() =>
                admin.DefineEventAsync(_yearId, "فعالية", "Event", CalendarEventCategory.SchoolEvent, new DateTime(2026, 8, 1), new DateTime(2026, 8, 2)));
        }

        // --- events --------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-CAL-002")]
        public async Task Defining_an_event_stores_its_bilingual_details()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            var evt = await admin.DefineEventAsync(
                _yearId, "اليوم الوطني", "National Day", CalendarEventCategory.National,
                new DateTime(2026, 9, 23), new DateTime(2026, 9, 23));

            Assert.Equal("National Day", db.CalendarEvents.Single(e => e.Id == evt.Id).NameEn);
        }

        [Fact]
        [BusinessRule("BR-CAL-002")]
        public async Task An_event_can_be_amended_in_place()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var evt = await admin.DefineEventAsync(
                _yearId, "رحلة", "Trip", CalendarEventCategory.SchoolEvent,
                new DateTime(2026, 10, 5), new DateTime(2026, 10, 5));

            await admin.UpdateEventAsync(
                evt.Id, "رحلة مدرسية", "School trip", CalendarEventCategory.SchoolEvent,
                new DateTime(2026, 10, 12), new DateTime(2026, 10, 13), CalendarAudience.StudentsOnly, isPortalVisible: false);

            var stored = db.CalendarEvents.Single(e => e.Id == evt.Id);
            Assert.Equal("School trip", stored.NameEn);
            Assert.Equal(new DateTime(2026, 10, 12), stored.StartDate);
            Assert.Equal(new DateTime(2026, 10, 13), stored.EndDate);
            Assert.Equal(CalendarAudience.StudentsOnly, stored.Audience);
            Assert.False(stored.IsPortalVisible);
            Assert.Single(db.CalendarEvents.Where(e => e.AcademicYearId == _yearId));
        }

        [Fact]
        [BusinessRule("BR-GLB-051")]
        public async Task An_event_cannot_be_amended_onto_a_date_outside_the_year()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var evt = await admin.DefineEventAsync(
                _yearId, "رحلة", "Trip", CalendarEventCategory.SchoolEvent,
                new DateTime(2026, 10, 5), new DateTime(2026, 10, 5));

            await Assert.ThrowsAsync<CalendarDateOutsideYearException>(() =>
                admin.UpdateEventAsync(evt.Id, "رحلة", "Trip", CalendarEventCategory.SchoolEvent, new DateTime(2027, 8, 1), new DateTime(2027, 8, 1)));
        }

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public async Task An_event_that_has_already_started_cannot_be_amended()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var evt = await admin.DefineEventAsync(
                _yearId, "رحلة", "Trip", CalendarEventCategory.SchoolEvent,
                new DateTime(2026, 9, 10), new DateTime(2026, 9, 10));

            // The clock moves past the event: it is now a record of something that happened.
            _clock.UtcNow = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

            await Assert.ThrowsAsync<CalendarPastDateEditException>(() =>
                admin.UpdateEventAsync(evt.Id, "رحلة", "Trip", CalendarEventCategory.SchoolEvent, new DateTime(2026, 10, 1), new DateTime(2026, 10, 1)));
        }

        // --- BR-GLB-005 cancellation ---------------------------------------------

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task Cancelling_an_event_deactivates_it_rather_than_removing_the_row()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var evt = await admin.DefineEventAsync(
                _yearId, "رحلة", "Trip", CalendarEventCategory.SchoolEvent,
                new DateTime(2026, 10, 5), new DateTime(2026, 10, 5));

            await admin.SetEventActiveAsync(evt.Id, isActive: false);

            var stored = Assert.Single(db.CalendarEvents.Where(e => e.AcademicYearId == _yearId));
            Assert.False(stored.IsActive);
        }

        [Fact]
        [BusinessRule("BR-GLB-006")]
        public async Task A_cancelled_event_can_be_reinstated()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var evt = await admin.DefineEventAsync(
                _yearId, "رحلة", "Trip", CalendarEventCategory.SchoolEvent,
                new DateTime(2026, 10, 5), new DateTime(2026, 10, 5));
            await admin.SetEventActiveAsync(evt.Id, isActive: false);

            await admin.SetEventActiveAsync(evt.Id, isActive: true);

            Assert.True(db.CalendarEvents.Single(e => e.Id == evt.Id).IsActive);
        }

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public async Task An_event_that_has_already_started_cannot_be_cancelled()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var evt = await admin.DefineEventAsync(
                _yearId, "رحلة", "Trip", CalendarEventCategory.SchoolEvent,
                new DateTime(2026, 9, 10), new DateTime(2026, 9, 10));

            _clock.UtcNow = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);

            await Assert.ThrowsAsync<CalendarPastDateEditException>(() => admin.SetEventActiveAsync(evt.Id, isActive: false));
        }

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public async Task A_calendar_event_cannot_be_hard_deleted()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);
            var evt = await admin.DefineEventAsync(
                _yearId, "رحلة", "Trip", CalendarEventCategory.SchoolEvent,
                new DateTime(2026, 10, 5), new DateTime(2026, 10, 5));

            using var second = CreateContext();
            second.CalendarEvents.Remove(second.CalendarEvents.Single(e => e.Id == evt.Id));

            await Assert.ThrowsAsync<HardDeleteForbiddenException>(() => second.SaveChangesAsync());
        }

        // --- BR-CAL-007 publication versioning -----------------------------------

        [Fact]
        [BusinessRule("BR-CAL-007")]
        public async Task Each_publish_increments_the_version_number()
        {
            using var db = CreateContext();
            var admin = new CalendarAdmin(db, _clock);

            var v1 = await admin.PublishAsync(_yearId, publishedByUserId: 7);
            var v2 = await admin.PublishAsync(_yearId, publishedByUserId: 7);

            Assert.Equal(1, v1.VersionNumber);
            Assert.Equal(2, v2.VersionNumber);
            Assert.Equal(2, db.CalendarVersions.Count(v => v.AcademicYearId == _yearId));
        }
    }
}
