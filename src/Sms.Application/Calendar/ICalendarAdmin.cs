using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Calendar;

namespace Sms.Application.Calendar
{
    /// <summary>doc/Modules/04 §8 "Year calendar board"/"Event manager" screens backing (screens deferred, the operations are core).</summary>
    public interface ICalendarAdmin
    {
        /// <summary>Throws <see cref="Common.Exceptions.CalendarPastDateEditException"/> or <see cref="Common.Exceptions.CalendarDateOutsideYearException"/>.</summary>
        Task<CalendarDay> DefineDayAsync(
            int academicYearId, DateTime date, DayType dayType, CalendarAudience audience = CalendarAudience.All,
            bool isProvisional = false, CancellationToken cancellationToken = default);

        Task<CalendarEvent> DefineEventAsync(
            int academicYearId, string nameAr, string nameEn, CalendarEventCategory category, DateTime startDate, DateTime endDate,
            CalendarAudience audience = CalendarAudience.All, bool isPortalVisible = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Amends an event in place (doc/Modules/04 §8.2). BR-CAL-004 blocks it in both
        /// directions: an event that already started cannot be rewritten, and it cannot be moved
        /// onto a past date. Throws <see cref="Common.Exceptions.CalendarPastDateEditException"/>
        /// or <see cref="Common.Exceptions.CalendarDateOutsideYearException"/>.
        /// </summary>
        Task<CalendarEvent> UpdateEventAsync(
            int calendarEventId, string nameAr, string nameEn, CalendarEventCategory category, DateTime startDate, DateTime endDate,
            CalendarAudience audience = CalendarAudience.All, bool isPortalVisible = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels an event, or reinstates one (BR-GLB-005 — there is no delete; BR-GLB-006 — it
        /// stays on the record). BR-CAL-004 still applies: an event that has already started
        /// cannot be cancelled out of history. Throws
        /// <see cref="Common.Exceptions.CalendarPastDateEditException"/>.
        /// </summary>
        Task<CalendarEvent> SetEventActiveAsync(int calendarEventId, bool isActive, CancellationToken cancellationToken = default);

        /// <summary>BR-CAL-007: snapshots a new publish version; the caller resolves whether unpublished edits exist.</summary>
        Task<CalendarVersion> PublishAsync(int academicYearId, int publishedByUserId, CancellationToken cancellationToken = default);
    }
}
