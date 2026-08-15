using System;

namespace Sms.Application.Calendar
{
    /// <summary>
    /// Pure BR-CAL-004 date check: past dates are blocked outright. The full
    /// rule also needs an impact-review workflow (existing attendance/exams
    /// on a date changing type) — deferred, since Attendance/Examinations
    /// don't exist yet to check impact against.
    /// </summary>
    public static class CalendarChangeGuard
    {
        public static bool IsPastDate(DateTime candidateDate, DateTime todayUtc)
            => candidateDate.Date < todayUtc.Date;
    }
}
