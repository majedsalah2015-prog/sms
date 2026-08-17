using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Timetable
{
    /// <summary>core.PeriodSlot (doc/Modules/15 §7, BR-TTB-001): one day x sequence slot within a TimetableShape, incl. breaks/assembly as non-teaching slots (IsBreak).</summary>
    [Audited(AuditTier.T2)]
    public class PeriodSlot : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int TimetableShapeId { get; set; }

        public DayOfWeek DayOfWeek { get; set; }

        public int SequenceNumber { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public bool IsBreak { get; set; }
    }
}
