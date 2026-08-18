using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Backup;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.SysAdmin;
using Sms.Domain.SysAdmin;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.SysAdmin
{
    /// <summary>
    /// Standalone admin operations — save themselves, no larger transaction
    /// to ride, except where a snapshot must be taken through IBackupAdmin
    /// first (BR-BAK-004). Named SysAdminService (not "SysAdmin") to avoid
    /// colliding with the Sms.Domain.SysAdmin/Sms.Application.SysAdmin
    /// namespace leaf segment — same collision-avoidance discipline as
    /// E-201's AdmissionApplication and E-607's ActivityProgram renames.
    /// </summary>
    public class SysAdminService : ISysAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IBackupAdmin _backupAdmin;
        private readonly IAuditAdmin _auditAdmin;

        public SysAdminService(AppDbContext db, IClock clock, IBackupAdmin backupAdmin, IAuditAdmin auditAdmin)
        {
            _db = db;
            _clock = clock;
            _backupAdmin = backupAdmin;
            _auditAdmin = auditAdmin;
        }

        // --- License (BR-SYS-006, O5) ----------------------------------------------------

        public async Task<LicenseState> DefineLicenseAsync(
            int schoolId, LicenseTier tier, int studentCountCap, DateTime expiresAtUtc, int graceDays, CancellationToken cancellationToken = default)
        {
            var license = new LicenseState { SchoolId = schoolId, Tier = tier, StudentCountCap = studentCountCap, ExpiresAtUtc = expiresAtUtc, GraceDays = graceDays };
            _db.LicenseStates.Add(license);
            await _db.SaveChangesAsync(cancellationToken);
            return license;
        }

        public async Task<LicenseStatus> GetLicenseStatusAsync(int schoolId, CancellationToken cancellationToken = default)
        {
            var license = await _db.LicenseStates.SingleAsync(l => l.SchoolId == schoolId, cancellationToken);
            return LicenseStatusEvaluator.ComputeStatus(_clock.UtcNow, license.ExpiresAtUtc, license.GraceDays);
        }

        // --- Maintenance (BR-SYS-007) -----------------------------------------------------

        public async Task<MaintenanceWindow> ScheduleMaintenanceAsync(
            DateTime startUtc, DateTime endUtc, string messageAr, string messageEn, bool isEmergency, TimeSpan minimumLeadTime, CancellationToken cancellationToken = default)
        {
            if (!MaintenanceLeadTimeGuard.HasSufficientLeadTime(_clock.UtcNow, startUtc, minimumLeadTime, isEmergency))
            {
                throw new InsufficientMaintenanceLeadTimeException();
            }

            var window = new MaintenanceWindow { StartUtc = startUtc, EndUtc = endUtc, MessageAr = messageAr, MessageEn = messageEn, IsEmergency = isEmergency };
            _db.MaintenanceWindows.Add(window);
            await _db.SaveChangesAsync(cancellationToken);
            return window;
        }

        // --- Import framework (BR-SYS-003) ------------------------------------------------

        public async Task<ImportBatch> StartDryRunAsync(int schoolId, string templateCode, int rowCount, int errorCount, CancellationToken cancellationToken = default)
        {
            var batch = new ImportBatch { SchoolId = schoolId, TemplateCode = templateCode, RowCount = rowCount, ErrorCount = errorCount };
            _db.ImportBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);
            return batch;
        }

        public async Task<ImportBatch> CommitAsync(int importBatchId, bool preOpSnapshotSucceeded = true, CancellationToken cancellationToken = default)
        {
            var batch = await _db.ImportBatches.SingleAsync(b => b.Id == importBatchId, cancellationToken);
            if (batch.Status != ImportBatchStatus.DryRun)
            {
                throw new ImportNotDryRunException(importBatchId);
            }

            await _backupAdmin.TakeSnapshotAsync($"pre-import-{importBatchId}", "ImportCommit", preOpSnapshotSucceeded, cancellationToken);

            batch.Status = ImportBatchStatus.Committed;
            batch.CommittedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return batch;
        }

        public async Task RollbackAsync(int importBatchId, CancellationToken cancellationToken = default)
        {
            var batch = await _db.ImportBatches.SingleAsync(b => b.Id == importBatchId, cancellationToken);
            var hasLaterCommit = await _db.ImportBatches.AnyAsync(
                b => b.SchoolId == batch.SchoolId && b.TemplateCode == batch.TemplateCode && b.Status == ImportBatchStatus.Committed
                     && b.CommittedAtUtc > batch.CommittedAtUtc, cancellationToken);
            if (hasLaterCommit)
            {
                throw new ImportRollbackWindowClosedException(importBatchId);
            }

            batch.Status = ImportBatchStatus.RolledBack;
            batch.RolledBackAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // --- Purge orchestration (BR-SYS-005, shared with BR-AUM-005) ---------------------

        public async Task<PurgeExecution> RequestPurgeAsync(
            PurgeDataClass dataClass, int? schoolId, DateTime horizonUtc, int requestedByUserId, CancellationToken cancellationToken = default)
        {
            var purge = new PurgeExecution { DataClass = dataClass, SchoolId = schoolId, HorizonUtc = horizonUtc, RequestedByUserId = requestedByUserId };
            _db.PurgeExecutions.Add(purge);
            await _db.SaveChangesAsync(cancellationToken);
            return purge;
        }

        public async Task<PurgeExecution> ApproveAndExecutePurgeAsync(
            int purgeExecutionId, int secondApproverUserId, bool preOpSnapshotSucceeded = true, CancellationToken cancellationToken = default)
        {
            var purge = await _db.PurgeExecutions.SingleAsync(p => p.Id == purgeExecutionId, cancellationToken);
            if (secondApproverUserId == purge.RequestedByUserId)
            {
                throw new SelfApprovalNotAllowedException(secondApproverUserId);
            }

            var hasActiveLegalHold = await _db.LegalHolds.AnyAsync(h => h.DataClass == purge.DataClass && h.ReleasedAtUtc == null, cancellationToken);
            var isAuditFrozen = purge.DataClass == PurgeDataClass.Audit && await _auditAdmin.IsAuditPurgeFrozenAsync(cancellationToken);
            if (!PurgeEligibilityEvaluator.IsEligible(purge.HorizonUtc, _clock.UtcNow, hasActiveLegalHold, isAuditFrozen))
            {
                purge.Status = PurgeExecutionStatus.Blocked;
                await _db.SaveChangesAsync(cancellationToken);
                throw new PurgeNotEligibleException(purgeExecutionId);
            }

            await _backupAdmin.TakeSnapshotAsync($"pre-purge-{purgeExecutionId}", "PurgeExecution", preOpSnapshotSucceeded, cancellationToken);

            purge.SecondApproverUserId = secondApproverUserId;
            purge.Status = PurgeExecutionStatus.Executed;
            purge.ExecutedAtUtc = _clock.UtcNow;
            purge.CertificateNo = $"PRG-{purgeExecutionId:D6}";
            await _db.SaveChangesAsync(cancellationToken);
            return purge;
        }

        public async Task<LegalHold> PlaceLegalHoldAsync(PurgeDataClass dataClass, string subjectReference, int placedByUserId, CancellationToken cancellationToken = default)
        {
            var hold = new LegalHold { DataClass = dataClass, SubjectReference = subjectReference, PlacedByUserId = placedByUserId, PlacedAtUtc = _clock.UtcNow };
            _db.LegalHolds.Add(hold);
            await _db.SaveChangesAsync(cancellationToken);
            return hold;
        }

        public async Task ReleaseLegalHoldAsync(int legalHoldId, CancellationToken cancellationToken = default)
        {
            var hold = await _db.LegalHolds.SingleAsync(h => h.Id == legalHoldId, cancellationToken);
            hold.ReleasedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // --- Diagnostics (BR-SYS-008) ------------------------------------------------------

        public async Task<DiagnosticsBundle> GenerateDiagnosticsBundleAsync(int? schoolId, int generatedByUserId, CancellationToken cancellationToken = default)
        {
            var bundle = new DiagnosticsBundle { SchoolId = schoolId, Reference = Guid.NewGuid().ToString("N"), GeneratedAtUtc = _clock.UtcNow };
            _db.DiagnosticsBundles.Add(bundle);
            await _db.SaveChangesAsync(cancellationToken);
            return bundle;
        }
    }
}
