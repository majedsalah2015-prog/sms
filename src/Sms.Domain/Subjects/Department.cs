using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Subjects
{
    /// <summary>
    /// core.Department (doc/Modules/07 §7, BR-SUB-007): Head-of-Department
    /// scoping for the marks-approval chain (WF-07) and reporting. Head
    /// assignment is a simple current-pointer in this slice, not
    /// effective-dated history like HomeroomAssignment — a reasonable
    /// simplification given no marks-approval workflow consumes it yet;
    /// revisit if WF-07 needs historical head-of-department attribution.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Department : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public LocalizedName Name { get; set; } = new();

        /// <summary>References sec.UserAccount (the head teacher); null until assigned.</summary>
        public int? HeadTeacherUserId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
