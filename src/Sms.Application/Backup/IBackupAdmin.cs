using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Backup;

namespace Sms.Application.Backup
{
    /// <summary>
    /// doc/Modules/35 §8 Protection status dashboard / Restore wizard / Policy
    /// viewer screens backing (screens deferred, the operations are core).
    /// Actual backup artifact creation/restore-execution is infrastructure
    /// tooling out of this slice's scope — this records and enforces the
    /// policy/verification/snapshot/restore-workflow rules doc §3 defines,
    /// so the eventual infra hook has real rules to report status against.
    /// </summary>
    public interface IBackupAdmin
    {
        Task<BackupPolicy> DefinePolicyAsync(
            BackupDeploymentClass deploymentClass, int retentionDailyCount, int retentionMonthlyCount, int retentionYearlyCount,
            bool onPremResponsibilityAcknowledged, CancellationToken cancellationToken = default);

        /// <summary>BR-BAK-001: Status is derived by BackupCompletenessEvaluator, never set ad hoc.</summary>
        Task<BackupRun> RecordRunAsync(
            int backupPolicyId, bool databaseIncluded, bool attachmentStoreIncluded, bool configurationIncluded,
            long sizeBytes, CancellationToken cancellationToken = default);

        Task<BackupVerificationRun> RecordVerificationAsync(
            int backupRunId, bool databaseRestoreOk, bool rowCountSanityOk, bool attachmentHashSampleOk, bool integrityCheckpointOk,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-BAK-004: records a labeled pre-operation snapshot. snapshotSucceeded
        /// carries the (simulated — no infra hook exists) outcome; throws
        /// <see cref="Common.Exceptions.SnapshotFailedException"/> on false so the
        /// caller (Purge/Import) aborts rather than proceeding unprotected.
        /// </summary>
        Task<SnapshotEvent> TakeSnapshotAsync(string label, string triggerOperation, bool snapshotSucceeded, CancellationToken cancellationToken = default);

        Task<RestoreCase> RequestRestoreAsync(
            int requestedByUserId, RestoreScope scope, int? schoolId, DateTime pointInTimeUtc, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidRestoreCaseTransitionException"/> off the BR-BAK-005 chain.</summary>
        Task<RestoreCase> AdvanceRestoreCaseAsync(int restoreCaseId, RestoreCaseStatus to, string? gapAnalysisNote = null, CancellationToken cancellationToken = default);
    }
}
