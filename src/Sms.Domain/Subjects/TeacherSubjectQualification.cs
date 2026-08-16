using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Subjects
{
    /// <summary>core.TeacherSubjectQualification (doc/Modules/07 §7, BR-SUB-006): which teachers may teach which subjects.</summary>
    [Audited(AuditTier.T2)]
    public class TeacherSubjectQualification : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>References sec.UserAccount.</summary>
        public int TeacherUserId { get; set; }

        public int SubjectId { get; set; }

        /// <summary>Optional stage restriction (e.g. qualified only for Secondary).</summary>
        public int? StageId { get; set; }

        public QualificationSource Source { get; set; }
    }
}
