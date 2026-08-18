using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Backup;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Backup;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Backup
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class BackupAdmin : IBackupAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;

        public BackupAdmin(AppDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<BackupPolicy> DefinePolicyAsync(
            BackupDeploymentClass deploymentClass, int retentionDailyCount, int retentionMonthlyCount, int retentionYearlyCount,
            bool onPremResponsibilityAcknowledged, CancellationToken cancellationToken = default)
        {
            var policy = new BackupPolicy
            {
                DeploymentClass = deploymentClass, RetentionDailyCount = retentionDailyCount, RetentionMonthlyCount = retentionMonthlyCount,
                RetentionYearlyCount = retentionYearlyCount, OnPremResponsibilityAcknowledged = onPremResponsibilityAcknowledged,
            };
            _db.BackupPolicies.Add(policy);
            await _db.SaveChangesAsync(cancellationToken);
            return policy;
        }

        public async Task<BackupRun> RecordRunAsync(
            int backupPolicyId, bool databaseIncluded, bool attachmentStoreIncluded, bool configurationIncluded,
            long sizeBytes, CancellationToken cancellationToken = default)
        {
            var isComplete = BackupCompletenessEvaluator.IsComplete(databaseIncluded, attachmentStoreIncluded, configurationIncluded);
            var run = new BackupRun
            {
                BackupPolicyId = backupPolicyId, DatabaseIncluded = databaseIncluded, AttachmentStoreIncluded = attachmentStoreIncluded,
                ConfigurationIncluded = configurationIncluded, Status = isComplete ? BackupRunStatus.Complete : BackupRunStatus.Degraded,
                SizeBytes = sizeBytes, RanAtUtc = _clock.UtcNow,
            };
            _db.BackupRuns.Add(run);
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task<BackupVerificationRun> RecordVerificationAsync(
            int backupRunId, bool databaseRestoreOk, bool rowCountSanityOk, bool attachmentHashSampleOk, bool integrityCheckpointOk,
            CancellationToken cancellationToken = default)
        {
            var verification = new BackupVerificationRun
            {
                BackupRunId = backupRunId, DatabaseRestoreOk = databaseRestoreOk, RowCountSanityOk = rowCountSanityOk,
                AttachmentHashSampleOk = attachmentHashSampleOk, IntegrityCheckpointOk = integrityCheckpointOk, CheckedAtUtc = _clock.UtcNow,
            };
            _db.BackupVerificationRuns.Add(verification);
            await _db.SaveChangesAsync(cancellationToken);
            return verification;
        }

        public async Task<SnapshotEvent> TakeSnapshotAsync(string label, string triggerOperation, bool snapshotSucceeded, CancellationToken cancellationToken = default)
        {
            var snapshot = new SnapshotEvent { Label = label, TriggerOperation = triggerOperation, Success = snapshotSucceeded, TakenAtUtc = _clock.UtcNow };
            _db.SnapshotEvents.Add(snapshot);
            await _db.SaveChangesAsync(cancellationToken);
            if (!snapshotSucceeded)
            {
                throw new SnapshotFailedException(triggerOperation);
            }

            return snapshot;
        }

        public async Task<RestoreCase> RequestRestoreAsync(
            int requestedByUserId, RestoreScope scope, int? schoolId, DateTime pointInTimeUtc, CancellationToken cancellationToken = default)
        {
            var restoreCase = new RestoreCase
            {
                RequestedByUserId = requestedByUserId, Scope = scope, SchoolId = schoolId, PointInTimeUtc = pointInTimeUtc, RequestedAtUtc = _clock.UtcNow,
            };
            _db.RestoreCases.Add(restoreCase);
            await _db.SaveChangesAsync(cancellationToken);
            return restoreCase;
        }

        public async Task<RestoreCase> AdvanceRestoreCaseAsync(int restoreCaseId, RestoreCaseStatus to, string? gapAnalysisNote = null, CancellationToken cancellationToken = default)
        {
            var restoreCase = await _db.RestoreCases.SingleAsync(c => c.Id == restoreCaseId, cancellationToken);
            if (!RestoreCaseStatusTransitions.CanTransition(restoreCase.Status, to))
            {
                throw new InvalidRestoreCaseTransitionException(restoreCaseId, restoreCase.Status.ToString(), to.ToString());
            }

            restoreCase.Status = to;
            if (gapAnalysisNote != null)
            {
                restoreCase.GapAnalysisNote = gapAnalysisNote;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return restoreCase;
        }
    }
}
