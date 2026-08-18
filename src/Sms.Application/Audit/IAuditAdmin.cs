using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Audit;

namespace Sms.Application.Audit
{
    /// <summary>
    /// doc/Modules/34 §8 Anomaly queue / Integrity dashboard screens backing
    /// (screens deferred, the operations are core). The explorer/record-
    /// history/security-console/retention-console surfaces are read layers
    /// over the framework's own AuditEntry/IntegrityCheckpoint stores and
    /// this module's own AnomalyHit/PurgeExecution — no new write operations
    /// needed beyond what's here.
    /// </summary>
    public interface IAuditAdmin
    {
        Task<AnomalyRule> DefineAnomalyRuleAsync(
            string code, string descriptionAr, string descriptionEn, AnomalySeverity severity, CancellationToken cancellationToken = default);

        Task<AnomalyHit> RecordAnomalyHitAsync(int anomalyRuleId, long auditEntryId, string contextJson, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.AnomalyHitAlreadyDispositionedException"/> unless the hit is still Open.</summary>
        Task<AnomalyHit> DispositionAnomalyHitAsync(
            int anomalyHitId, AnomalyHitStatus disposition, int dispositionedByUserId, string note, CancellationToken cancellationToken = default);

        /// <summary>BR-AUM-001: recomputes and persists the verification result via IntegrityCheckpointService.VerifyAsync.</summary>
        Task<IntegrityVerificationRun> RunIntegrityVerificationAsync(long integrityCheckpointId, CancellationToken cancellationToken = default);

        Task ResolveVerificationFailureAsync(int integrityVerificationRunId, CancellationToken cancellationToken = default);

        /// <summary>BR-AUM-001: true while any failed verification run remains unresolved — freezes audit-data purge (consumed by ISysAdmin's PurgeEligibilityEvaluator check).</summary>
        Task<bool> IsAuditPurgeFrozenAsync(CancellationToken cancellationToken = default);
    }
}
