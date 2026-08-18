using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.SysAdmin
{
    /// <summary>
    /// ops.PurgeExecution (BR-SYS-005, reused for BR-AUM-005's audit-data
    /// purge too — one generic certified-purge concept rather than a
    /// parallel entity per data class, the same reuse-over-duplication call
    /// as PositionLookupId reusing LookupValue back in E-203). Dual-
    /// confirmed (Sys Admin + Registrar/Auditor per data class), snapshot-
    /// gated (BR-BAK-004), and legal-hold aware. Not ISchoolScoped:
    /// DataClass.Audit purges target the unscoped audit store; SchoolId is
    /// nullable context only.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class PurgeExecution : AuditableEntity
    {
        public PurgeDataClass DataClass { get; set; }

        public int? SchoolId { get; set; }

        public DateTime HorizonUtc { get; set; }

        public PurgeExecutionStatus Status { get; set; } = PurgeExecutionStatus.Requested;

        public int RequestedByUserId { get; set; }

        public int? SecondApproverUserId { get; set; }

        public string? CertificateNo { get; set; }

        public DateTime? ExecutedAtUtc { get; set; }
    }
}
