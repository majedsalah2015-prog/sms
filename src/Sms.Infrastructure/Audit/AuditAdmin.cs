using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Audit
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class AuditAdmin : IAuditAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IntegrityCheckpointService _checkpointService;

        public AuditAdmin(AppDbContext db, IClock clock, IntegrityCheckpointService checkpointService)
        {
            _db = db;
            _clock = clock;
            _checkpointService = checkpointService;
        }

        public async Task<AnomalyRule> DefineAnomalyRuleAsync(
            string code, string descriptionAr, string descriptionEn, AnomalySeverity severity, CancellationToken cancellationToken = default)
        {
            var rule = new AnomalyRule { Code = code, DescriptionAr = descriptionAr, DescriptionEn = descriptionEn, Severity = severity };
            _db.AnomalyRules.Add(rule);
            await _db.SaveChangesAsync(cancellationToken);
            return rule;
        }

        public async Task<AnomalyHit> RecordAnomalyHitAsync(int anomalyRuleId, long auditEntryId, string contextJson, CancellationToken cancellationToken = default)
        {
            var hit = new AnomalyHit { AnomalyRuleId = anomalyRuleId, AuditEntryId = auditEntryId, ContextJson = contextJson, DetectedAtUtc = _clock.UtcNow };
            _db.AnomalyHits.Add(hit);
            await _db.SaveChangesAsync(cancellationToken);
            return hit;
        }

        public async Task<AnomalyHit> DispositionAnomalyHitAsync(
            int anomalyHitId, AnomalyHitStatus disposition, int dispositionedByUserId, string note, CancellationToken cancellationToken = default)
        {
            var hit = await _db.AnomalyHits.SingleAsync(h => h.Id == anomalyHitId, cancellationToken);
            if (!AnomalyDispositionPolicy.CanDispose(hit.Status))
            {
                throw new AnomalyHitAlreadyDispositionedException(anomalyHitId);
            }

            hit.Status = disposition;
            hit.DispositionNote = note;
            hit.DispositionedByUserId = dispositionedByUserId;
            hit.DispositionedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return hit;
        }

        public async Task<IntegrityVerificationRun> RunIntegrityVerificationAsync(long integrityCheckpointId, CancellationToken cancellationToken = default)
        {
            var passed = await _checkpointService.VerifyAsync(integrityCheckpointId, cancellationToken);
            var run = new IntegrityVerificationRun { IntegrityCheckpointId = integrityCheckpointId, Passed = passed, RanAtUtc = _clock.UtcNow, IsResolved = passed };
            _db.IntegrityVerificationRuns.Add(run);
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task ResolveVerificationFailureAsync(int integrityVerificationRunId, CancellationToken cancellationToken = default)
        {
            var run = await _db.IntegrityVerificationRuns.SingleAsync(r => r.Id == integrityVerificationRunId, cancellationToken);
            run.IsResolved = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>BR-AUM-001: any unresolved failed run freezes audit-data purge until an Auditor resolves it.</summary>
        public Task<bool> IsAuditPurgeFrozenAsync(CancellationToken cancellationToken = default)
            => _db.IntegrityVerificationRuns.AnyAsync(r => !r.Passed && !r.IsResolved, cancellationToken);
    }
}
