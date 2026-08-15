using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Calendar
{
    /// <summary>
    /// core.CalendarDay (doc/Modules/04 §7): year × date → day type,
    /// materialized for fast joins by Attendance/Timetable — consumers
    /// reference this, never re-implement week logic.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class CalendarDay : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public DateTime Date { get; set; }

        public DayType DayType { get; set; }

        public CalendarAudience Audience { get; set; } = CalendarAudience.All;

        public CalendarDaySource Source { get; set; } = CalendarDaySource.Rule;

        /// <summary>BR-CAL-005: a Hijri-mapped religious holiday entered before the Gregorian date is confirmed.</summary>
        public bool IsProvisional { get; set; }
    }
}
