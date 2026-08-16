using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Teachers
{
    /// <summary>
    /// core.TeacherAssignment (doc/Modules/13 §7, BR-TCH-002): teacher ×
    /// curriculum offering × section × year — the atomic unit granting
    /// marks-entry/attendance-entry rights (Modules 14/17, not wired —
    /// no consumer exists yet). Effective-dated so mid-year teacher
    /// changes preserve history (BR-TCH-007's continuity flow itself —
    /// marksheet/timetable re-pointing, parent notification — is
    /// deferred; this just gives the row shape it will need).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class TeacherAssignment : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int TeacherProfileId { get; set; }

        public int CurriculumOfferingId { get; set; }

        public int SectionId { get; set; }

        public TeacherRole Role { get; set; } = TeacherRole.Primary;

        public DateTime EffectiveFromUtc { get; set; }

        public DateTime? EffectiveToUtc { get; set; }
    }
}
