using System;
using Sms.Domain.Common;

namespace Sms.Domain.Audit
{
    /// <summary>
    /// aud.AnomalyHit (BR-AUM-002): a rule firing against the audit stream —
    /// creates a review item for the Auditor queue, never an automatic
    /// reversal. Not ISchoolScoped: rides AuditEntry's own unfiltered
    /// tenancy rather than re-deriving it.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class AnomalyHit : AuditableEntity
    {
        public int AnomalyRuleId { get; set; }

        public long AuditEntryId { get; set; }

        public string ContextJson { get; set; } = string.Empty;

        public AnomalyHitStatus Status { get; set; } = AnomalyHitStatus.Open;

        public string? DispositionNote { get; set; }

        public int? DispositionedByUserId { get; set; }

        public DateTime? DispositionedAtUtc { get; set; }

        public DateTime DetectedAtUtc { get; set; }
    }
}
