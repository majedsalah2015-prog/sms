using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.SysAdmin;

namespace Sms.Application.SysAdmin
{
    /// <summary>
    /// doc/Modules/36 §8 License panel / Import workbench / Purge center /
    /// Maintenance banner screens backing (screens deferred, the operations
    /// are core). User/role/permission administration and the job console
    /// are NOT here — those are E-003's and E-011's own admin surfaces,
    /// reused as-is (BR-SYS-001/002/004 add no new semantics per doc §3).
    /// Import's actual file parsing/schema validation/dedup engagement is a
    /// standalone content-authoring concern out of scope — this models the
    /// batch lifecycle that framework must obey.
    /// </summary>
    public interface ISysAdmin
    {
        Task<LicenseState> DefineLicenseAsync(
            int schoolId, LicenseTier tier, int studentCountCap, DateTime expiresAtUtc, int graceDays, CancellationToken cancellationToken = default);

        /// <summary>BR-SYS-006: status is derived fresh every call via LicenseStatusEvaluator — never stored, so it can never drift from the clock.</summary>
        Task<LicenseStatus> GetLicenseStatusAsync(int schoolId, CancellationToken cancellationToken = default);

        /// <summary>BR-SYS-007: throws <see cref="Common.Exceptions.InsufficientMaintenanceLeadTimeException"/> unless MaintenanceLeadTimeGuard is satisfied (always satisfied for an emergency window).</summary>
        Task<MaintenanceWindow> ScheduleMaintenanceAsync(
            DateTime startUtc, DateTime endUtc, string messageAr, string messageEn, bool isEmergency, TimeSpan minimumLeadTime, CancellationToken cancellationToken = default);

        Task<ImportBatch> StartDryRunAsync(int schoolId, string templateCode, int rowCount, int errorCount, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-SYS-003/BR-BAK-004: takes a labeled pre-op snapshot first via
        /// IBackupAdmin; throws <see cref="Common.Exceptions.SnapshotFailedException"/>
        /// if it fails, <see cref="Common.Exceptions.ImportNotDryRunException"/> if the
        /// batch isn't in DryRun.
        /// </summary>
        Task<ImportBatch> CommitAsync(int importBatchId, bool preOpSnapshotSucceeded = true, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.ImportRollbackWindowClosedException"/> once a later batch has committed against the same template.</summary>
        Task RollbackAsync(int importBatchId, CancellationToken cancellationToken = default);

        Task<PurgeExecution> RequestPurgeAsync(
            PurgeDataClass dataClass, int? schoolId, DateTime horizonUtc, int requestedByUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-SYS-005/BR-AUM-005: dual-confirms (throws
        /// <see cref="Common.Exceptions.SelfApprovalNotAllowedException"/> on a
        /// self-approval attempt), checks PurgeEligibilityEvaluator (throws
        /// <see cref="Common.Exceptions.PurgeNotEligibleException"/>), takes a pre-op
        /// snapshot via IBackupAdmin first (throws
        /// <see cref="Common.Exceptions.SnapshotFailedException"/> on failure).
        /// </summary>
        Task<PurgeExecution> ApproveAndExecutePurgeAsync(
            int purgeExecutionId, int secondApproverUserId, bool preOpSnapshotSucceeded = true, CancellationToken cancellationToken = default);

        Task<LegalHold> PlaceLegalHoldAsync(PurgeDataClass dataClass, string subjectReference, int placedByUserId, CancellationToken cancellationToken = default);

        Task ReleaseLegalHoldAsync(int legalHoldId, CancellationToken cancellationToken = default);

        Task<DiagnosticsBundle> GenerateDiagnosticsBundleAsync(int? schoolId, int generatedByUserId, CancellationToken cancellationToken = default);
    }
}
