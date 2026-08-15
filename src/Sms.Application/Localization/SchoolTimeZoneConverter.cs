using System;

namespace Sms.Application.Localization
{
    /// <summary>
    /// doc 02 §6: store UTC, display in school TZ, attendance "day"
    /// boundaries computed in school TZ. Takes a resolved
    /// <see cref="TimeZoneInfo"/> rather than a raw id string — resolving an
    /// id (Windows vs IANA, OS tz database) is the caller's/Infrastructure's
    /// job; this stays a pure, deterministically testable function. No
    /// School entity exists yet (S1) to carry the id, so callers supply it
    /// explicitly for now.
    /// </summary>
    public static class SchoolTimeZoneConverter
    {
        public static DateTime ToUtc(DateTime schoolLocalTime, TimeZoneInfo schoolTimeZone)
        {
            var unspecified = DateTime.SpecifyKind(schoolLocalTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, schoolTimeZone);
        }

        public static DateTime ToSchoolLocal(DateTime utcTime, TimeZoneInfo schoolTimeZone)
        {
            var utc = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, schoolTimeZone);
        }

        /// <summary>The UTC [start, end) window that one school-local calendar date spans — the attendance "day" (doc 02 §6).</summary>
        public static (DateTime StartUtc, DateTime EndUtc) GetSchoolDayBoundariesUtc(DateTime schoolLocalDate, TimeZoneInfo schoolTimeZone)
        {
            var startLocal = schoolLocalDate.Date;
            var endLocal = startLocal.AddDays(1);
            return (ToUtc(startLocal, schoolTimeZone), ToUtc(endLocal, schoolTimeZone));
        }
    }
}
