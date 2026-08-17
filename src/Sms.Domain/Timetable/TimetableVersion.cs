using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Timetable
{
    /// <summary>core.TimetableVersion (doc/Modules/15 §7, BR-TTB-002): a year's (optionally term's) timetable draft-through-published lifecycle.</summary>
    [Audited(AuditTier.T1)]
    public class TimetableVersion : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int? TermId { get; set; }

        public TimetableVersionStatus Status { get; set; } = TimetableVersionStatus.Draft;

        public DateTime? PublishedAtUtc { get; set; }
    }
}
