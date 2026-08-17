using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Activities
{
    /// <summary>ppl.ProgramEnrollment (doc/Modules/29 §7, BR-ACT-002): request -> eligibility/capacity -> consent -> fee -> active.</summary>
    [Audited(AuditTier.T2)]
    public class ProgramEnrollment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ProgramId { get; set; }

        public int StudentId { get; set; }

        public ProgramEnrollmentStatus Status { get; set; } = ProgramEnrollmentStatus.Requested;

        public int? ChargeId { get; set; }

        public DateTime RequestedAtUtc { get; set; }

        public string? WithdrawalReason { get; set; }
    }
}
