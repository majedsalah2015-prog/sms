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
