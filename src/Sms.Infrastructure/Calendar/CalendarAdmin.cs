using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Calendar;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Calendar;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Calendar
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class CalendarAdmin : ICalendarAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;

        public CalendarAdmin(AppDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<CalendarDay> DefineDayAsync(
            int academicYearId, DateTime date, DayType dayType, CalendarAudience audience = CalendarAudience.All,
            bool isProvisional = false, CancellationToken cancellationToken = default)
        {
            if (CalendarChangeGuard.IsPastDate(date, _clock.UtcNow))
            {
                throw new CalendarPastDateEditException(date);
            }

            await EnsureWithinYearAsync(academicYearId, date, date, cancellationToken);

            var day = await _db.CalendarDays.SingleOrDefaultAsync(
                d => d.AcademicYearId == academicYearId && d.Date == date.Date, cancellationToken);
            if (day == null)
            {
                day = new CalendarDay { AcademicYearId = academicYearId, Date = date.Date };
                _db.CalendarDays.Add(day);
            }

            day.DayType = dayType;
            day.Audience = audience;
            day.Source = CalendarDaySource.Manual;
            day.IsProvisional = isProvisional;

            await _db.SaveChangesAsync(cancellationToken);
            return day;
        }

        /// <summary>
        /// One range, one transaction. Every day is validated first — a range that reaches into
        /// the past or past the year end is refused whole, before a single row is written — and
        /// the rows already present are loaded in one query rather than one lookup per day.
        /// </summary>
        public async Task<int> DefineDaysAsync(
            int academicYearId, DateTime startDate, DateTime endDate, DayType dayType, CalendarAudience audience = CalendarAudience.All,
            bool isProvisional = false, CancellationToken cancellationToken = default)
        {
            var from = startDate.Date;
            var to = endDate.Date;
            if (to < from)
            {
                (from, to) = (to, from);
            }

            if (CalendarChangeGuard.IsPastDate(from, _clock.UtcNow))
            {
                throw new CalendarPastDateEditException(from);
            }

            await EnsureWithinYearAsync(academicYearId, from, to, cancellationToken);

            var existing = await _db.CalendarDays
                .Where(d => d.AcademicYearId == academicYearId && d.Date >= from && d.Date <= to)
                .ToDictionaryAsync(d => d.Date.Date, cancellationToken);

            var painted = 0;
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                if (!existing.TryGetValue(date, out var day))
                {
                    day = new CalendarDay { AcademicYearId = academicYearId, Date = date };
                    _db.CalendarDays.Add(day);
                }

                day.DayType = dayType;
                day.Audience = audience;
                day.Source = CalendarDaySource.Manual;
                day.IsProvisional = isProvisional;
                painted++;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return painted;
        }

        public async Task<CalendarDay?> ConfirmProvisionalDayAsync(int academicYearId, DateTime date, CancellationToken cancellationToken = default)
        {
            if (CalendarChangeGuard.IsPastDate(date, _clock.UtcNow))
            {
                throw new CalendarPastDateEditException(date);
            }

            var day = await _db.CalendarDays.SingleOrDefaultAsync(
                d => d.AcademicYearId == academicYearId && d.Date == date.Date, cancellationToken);
            if (day == null)
            {
                return null;
            }

            if (!day.IsProvisional)
            {
                return day;
            }

            day.IsProvisional = false;
            await _db.SaveChangesAsync(cancellationToken);
            return day;
        }

        public async Task<CalendarEvent> DefineEventAsync(
            int academicYearId, string nameAr, string nameEn, CalendarEventCategory category, DateTime startDate, DateTime endDate,
            CalendarAudience audience = CalendarAudience.All, bool isPortalVisible = true, CancellationToken cancellationToken = default)
        {
            if (CalendarChangeGuard.IsPastDate(startDate, _clock.UtcNow))
            {
                throw new CalendarPastDateEditException(startDate);
            }

            await EnsureWithinYearAsync(academicYearId, startDate, endDate, cancellationToken);

            var calendarEvent = new CalendarEvent
            {
                AcademicYearId = academicYearId,
                NameAr = nameAr,
                NameEn = nameEn,
                Category = category,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Audience = audience,
                IsPortalVisible = isPortalVisible,
            };
            _db.CalendarEvents.Add(calendarEvent);

            await _db.SaveChangesAsync(cancellationToken);
            return calendarEvent;
        }

        public async Task<CalendarEvent> UpdateEventAsync(
            int calendarEventId, string nameAr, string nameEn, CalendarEventCategory category, DateTime startDate, DateTime endDate,
            CalendarAudience audience = CalendarAudience.All, bool isPortalVisible = true, CancellationToken cancellationToken = default)
        {
            var calendarEvent = await _db.CalendarEvents.SingleAsync(e => e.Id == calendarEventId, cancellationToken);

            // Both ends of BR-CAL-004. The stored date matters as much as the new one: an event
            // that has already run is history, and renaming it is how the record of what the
            // school actually did gets rewritten after the fact.
            if (CalendarChangeGuard.IsPastDate(calendarEvent.StartDate, _clock.UtcNow))
            {
                throw new CalendarPastDateEditException(calendarEvent.StartDate);
            }

            if (CalendarChangeGuard.IsPastDate(startDate, _clock.UtcNow))
            {
                throw new CalendarPastDateEditException(startDate);
            }

            await EnsureWithinYearAsync(calendarEvent.AcademicYearId, startDate, endDate, cancellationToken);

            calendarEvent.NameAr = nameAr;
            calendarEvent.NameEn = nameEn;
            calendarEvent.Category = category;
            calendarEvent.StartDate = startDate.Date;
            calendarEvent.EndDate = endDate.Date;
            calendarEvent.Audience = audience;
            calendarEvent.IsPortalVisible = isPortalVisible;

            await _db.SaveChangesAsync(cancellationToken);
            return calendarEvent;
        }

        public async Task<CalendarEvent> SetEventActiveAsync(int calendarEventId, bool isActive, CancellationToken cancellationToken = default)
        {
            var calendarEvent = await _db.CalendarEvents.SingleAsync(e => e.Id == calendarEventId, cancellationToken);

            if (CalendarChangeGuard.IsPastDate(calendarEvent.StartDate, _clock.UtcNow))
            {
                throw new CalendarPastDateEditException(calendarEvent.StartDate);
            }

            calendarEvent.IsActive = isActive;

            await _db.SaveChangesAsync(cancellationToken);
            return calendarEvent;
        }

        public async Task<CalendarVersion> PublishAsync(int academicYearId, int publishedByUserId, CancellationToken cancellationToken = default)
        {
            var lastVersionNumber = await _db.CalendarVersions
                .Where(v => v.AcademicYearId == academicYearId)
                .Select(v => (int?)v.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;

            var version = new CalendarVersion
            {
                AcademicYearId = academicYearId,
                VersionNumber = lastVersionNumber + 1,
                PublishedAtUtc = _clock.UtcNow,
                PublishedByUserId = publishedByUserId,
            };
            _db.CalendarVersions.Add(version);

            await _db.SaveChangesAsync(cancellationToken);
            return version;
        }

        private async Task EnsureWithinYearAsync(int academicYearId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == academicYearId, cancellationToken);
            if (startDate.Date < year.StartDate.Date || endDate.Date > year.EndDate.Date)
            {
                throw new CalendarDateOutsideYearException(startDate);
            }
        }
    }
}
