using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Timetable
{
    /// <summary>
    /// core.TimetableShape (doc/Modules/15 §7, BR-TTB-001): the stage's
    /// day/period template for a year — year-versioned. Individual day
    /// templates (different period counts per weekday, e.g. a short
    /// Friday) live on <see cref="PeriodSlot"/> via its own DayOfWeek,
    /// not here (doc §14 open question #3's "day-level templates" — the
    /// shape is just the umbrella a year's slots hang off).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class TimetableShape : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int StageId { get; set; }
    }
}
