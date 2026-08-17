using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// ppl.RescheduleCase (doc/Modules/20 §7, BR-INS-005): a proposal to
    /// re-split the UNPAID remainder of a schedule. Proposed → Approved |
    /// Rejected (P3; RequiresPrincipal marks the P4 escalation when the
    /// extension exceeds the configured months or crosses year-end —
    /// recorded, not routed: same status-only workflow substitution as
    /// every other WF in this build). ProposedScheduleJson holds the new
    /// split; on approval the superseded installments are flagged and the
    /// new ones materialize.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class RescheduleCase : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int PlanAssignmentId { get; set; }

        public int ProposedByUserId { get; set; }

        public string Reason { get; set; } = string.Empty;

        public string ProposedScheduleJson { get; set; } = string.Empty;

        public decimal RemainderAmount { get; set; }

        public bool RequiresPrincipal { get; set; }

        public RescheduleCaseStatus Status { get; set; } = RescheduleCaseStatus.Proposed;

        public DateTime ProposedAtUtc { get; set; }

        public DateTime? DecidedAtUtc { get; set; }

        public string? DecisionReason { get; set; }
    }
}
