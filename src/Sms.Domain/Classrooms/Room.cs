using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Domain.Grades;

namespace Sms.Domain.Classrooms
{
    /// <summary>
    /// core.Room (doc/Modules/08 §7, BR-ROM-001/002/003): the physical space
    /// Sections get a home in and Timetable places sessions in. WingTag
    /// reuses <see cref="GenderPolicy"/> — segregated-campus wings are the
    /// same Mixed/Boys/Girls semantics as grade/section gender policy
    /// (BR-ROM-003 explicitly ties to BR-GRD-004), so
    /// <see cref="Application.Grades.GenderPolicyNarrowing"/> is reusable
    /// for wing-vs-section-gender compatibility rather than a parallel enum.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class Room : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int FloorId { get; set; }

        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        /// <summary>References core.LookupValue, category "RoomType" (classroom/lab/gym/hall/…).</summary>
        public int RoomTypeLookupId { get; set; }

        /// <summary>Teaching capacity.</summary>
        public int StandardCapacity { get; set; }

        /// <summary>Spaced exam seating — always ≤ StandardCapacity (BR-ROM-002).</summary>
        public int ExamCapacity { get; set; }

        public GenderPolicy WingTag { get; set; } = GenderPolicy.Mixed;

        public bool IsActive { get; set; } = true;
    }
}
