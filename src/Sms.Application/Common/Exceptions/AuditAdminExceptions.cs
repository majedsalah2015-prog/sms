using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-AUM-002: a hit that is already Dismissed/Escalated cannot be dispositioned again.</summary>
    public class AnomalyHitAlreadyDispositionedException : InvalidOperationException
    {
        public AnomalyHitAlreadyDispositionedException(int anomalyHitId)
            : base($"Anomaly hit {anomalyHitId} has already been dispositioned (BR-AUM-002).")
        {
        }
    }

    /// <summary>BR-AUM-001: an unresolved failed integrity verification freezes audit-affecting maintenance (purges) until an Auditor resolves it.</summary>
    public class AuditPurgeFrozenException : InvalidOperationException
    {
        public AuditPurgeFrozenException()
            : base("Audit-data purge is frozen pending investigation of a failed integrity verification run (BR-AUM-001).")
        {
        }
    }
}
