using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Domain.Grades;

namespace Sms.Domain.Sections
{
    /// <summary>
    /// core.Section (doc/Modules/06 §7, BR-SCN-001/002/003): a class group
    /// within a grade for one academic year.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Section : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int GradeYearProfileId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public int Capacity { get; set; }

        /// <summary>BR-SCN-003: narrows the grade's policy, never widens.</summary>
        public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Mixed;

        /// <summary>References core.Room (Module 08/E-104 — real FK as of that slice; was an unconstrained forward reference before).</summary>
        public int? DefaultClassroomId { get; set; }

        public SectionStatus Status { get; set; } = SectionStatus.Active;
    }
}
