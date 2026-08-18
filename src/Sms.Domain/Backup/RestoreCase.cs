using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Backup
{
    /// <summary>
    /// ops.RestoreCase (BR-BAK-005): support-gated restore workflow — never
    /// self-service. RequestedByUserId requests; product support executes.
    /// SchoolId is only meaningful when Scope == Tenant.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class RestoreCase : AuditableEntity
    {
        public int RequestedByUserId { get; set; }

        public RestoreScope Scope { get; set; }

        public int? SchoolId { get; set; }

        public DateTime PointInTimeUtc { get; set; }

        public RestoreCaseStatus Status { get; set; } = RestoreCaseStatus.Requested;

        public string? GapAnalysisNote { get; set; }

        public string? CertificateNo { get; set; }

        public DateTime RequestedAtUtc { get; set; }
    }
}
