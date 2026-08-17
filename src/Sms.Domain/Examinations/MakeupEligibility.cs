using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Examinations
{
    /// <summary>core.MakeupEligibility (doc/Modules/16 §7, BR-EXM-008): system-derived from excused/medical exam absences, manually extendable with permission (T1).</summary>
    [Audited(AuditTier.T1)]
    public class MakeupEligibility : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ExamId { get; set; }

        public int EnrollmentId { get; set; }

        public bool IsSystemDerived { get; set; }

        public int? ApprovedByUserId { get; set; }
    }
}
