using System;
using Sms.Domain.Common;

namespace Sms.Domain.Audit
{
    /// <summary>
    /// aud.IntegrityVerificationRun (doc/Modules/34 §7, BR-AUM-001): a
    /// persisted result wrapping
    /// Sms.Infrastructure.Audit.IntegrityCheckpointService.VerifyAsync,
    /// scheduled or on-demand. Named distinctly from Module 35's own
    /// "VerificationRun" concept (backup test-restore) to avoid a same-name
    /// collision across two different modules' verification runs.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class IntegrityVerificationRun : AuditableEntity
    {
        public long IntegrityCheckpointId { get; set; }

        public bool Passed { get; set; }

        public DateTime RanAtUtc { get; set; }

        /// <summary>BR-AUM-001: a failed run freezes audit-affecting maintenance (purges) until an Auditor resolves it.</summary>
        public bool IsResolved { get; set; }
    }
}
