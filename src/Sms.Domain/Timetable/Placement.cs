using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Timetable
{
    /// <summary>
    /// core.Placement (doc/Modules/15 §7, BR-TTB-003): section x period-slot
    /// x offering x teacher(+room) within a version. TeacherProfileId must
    /// hold a matching TeacherAssignment for (offering, section) per
    /// BR-TCH-002 — validated by the admin service at placement time, not
    /// by a DB constraint (same "engine enforces, DB just stores" split
    /// as every other cross-entity business rule in this codebase).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Placement : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int TimetableVersionId { get; set; }

        public int SectionId { get; set; }

        public int PeriodSlotId { get; set; }

        public int CurriculumOfferingId { get; set; }

        public int TeacherProfileId { get; set; }

        public int? RoomId { get; set; }
    }
}
