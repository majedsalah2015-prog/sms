using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Sections
{
    /// <summary>
    /// core.HomeroomAssignment (BR-SCN-004): at most one CURRENT (open-ended)
    /// assignment per section per year — enforced by a filtered unique index,
    /// not just convention. Reassignment is effective-dated so a mid-year
    /// handover stays visible in history.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class HomeroomAssignment : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int SectionId { get; set; }

        /// <summary>References sec.UserAccount — teachers already have accounts (E-003); no forward-reference issue here.</summary>
        public int TeacherUserId { get; set; }

        public DateTime EffectiveFromUtc { get; set; }

        /// <summary>Null = the current homeroom teacher for this section.</summary>
        public DateTime? EffectiveToUtc { get; set; }
    }
}
