using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.SysAdmin;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Backup;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.SysAdmin;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S7/E-704 (SysAdmin, doc/Modules/36, BR-SYS-003/005/006/007) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class SysAdminServiceTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 1;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public SysAdminServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private SysAdminService CreateSut(AppDbContext db)
            => new(db, _clock, new BackupAdmin(db, _clock), new AuditAdmin(db, _clock, new IntegrityCheckpointService(db, _clock)));

        // --- BR-SYS-006 license status (O5) --------------------------------------------

        [Fact]
        [BusinessRule("BR-SYS-006")]
        public async Task A_license_within_its_term_is_active()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            await sut.DefineLicenseAsync(1, LicenseTier.Professional, studentCountCap: 500, expiresAtUtc: _clock.UtcNow.AddYears(1), graceDays: 30);

            var status = await sut.GetLicenseStatusAsync(1);

            Assert.Equal(LicenseStatus.Active, status);
        }

        [Fact]
        [BusinessRule("BR-SYS-006")]
        public async Task An_expired_license_past_its_grace_window_is_read_only_not_locked_out()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            await sut.DefineLicenseAsync(1, LicenseTier.Essentials, studentCountCap: 200, expiresAtUtc: _clock.UtcNow.AddDays(-40), graceDays: 30);

            var status = await sut.GetLicenseStatusAsync(1);

            Assert.Equal(LicenseStatus.ReadOnly, status);
        }

        // --- BR-SYS-007 maintenance lead time -------------------------------------------

        [Fact]
        [BusinessRule("BR-SYS-007")]
        public async Task Scheduling_with_sufficient_lead_time_succeeds()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);

            var window = await sut.ScheduleMaintenanceAsync(
                _clock.UtcNow.AddDays(3), _clock.UtcNow.AddDays(3).AddHours(2), "صيانة", "Maintenance",
                isEmergency: false, minimumLeadTime: TimeSpan.FromDays(2));

            Assert.False(window.IsEmergency);
        }

        [Fact]
        [BusinessRule("BR-SYS-007")]
        public async Task Scheduling_without_sufficient_lead_time_is_rejected_unless_emergency()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);

            await Assert.ThrowsAsync<InsufficientMaintenanceLeadTimeException>(() =>
                sut.ScheduleMaintenanceAsync(_clock.UtcNow.AddHours(1), _clock.UtcNow.AddHours(3), "صيانة", "Maintenance", isEmergency: false, minimumLeadTime: TimeSpan.FromDays(2)));

            var emergency = await sut.ScheduleMaintenanceAsync(
                _clock.UtcNow.AddHours(1), _clock.UtcNow.AddHours(3), "صيانة طارئة", "Emergency maintenance", isEmergency: true, minimumLeadTime: TimeSpan.FromDays(2));
            Assert.True(emergency.IsEmergency);
        }

        // --- BR-SYS-003 import lifecycle + BR-BAK-004 snapshot gate --------------------

        [Fact]
        [BusinessRule("BR-SYS-003")]
        public async Task Committing_a_dry_run_batch_takes_a_snapshot_first_and_succeeds()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            var batch = await sut.StartDryRunAsync(1, "students-parents", rowCount: 120, errorCount: 0);

            var committed = await sut.CommitAsync(batch.Id);

            Assert.Equal(ImportBatchStatus.Committed, committed.Status);
            Assert.Single(db.SnapshotEvents.Where(s => s.TriggerOperation == "ImportCommit"));
        }

        [Fact]
        [BusinessRule("BR-BAK-004")]
        public async Task Committing_is_blocked_when_the_pre_op_snapshot_fails()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            var batch = await sut.StartDryRunAsync(1, "students-parents", rowCount: 120, errorCount: 0);

            await Assert.ThrowsAsync<SnapshotFailedException>(() => sut.CommitAsync(batch.Id, preOpSnapshotSucceeded: false));

            Assert.Equal(ImportBatchStatus.DryRun, db.ImportBatches.Single(b => b.Id == batch.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-SYS-003")]
        public async Task Committing_a_batch_twice_is_rejected()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            var batch = await sut.StartDryRunAsync(1, "students-parents", rowCount: 120, errorCount: 0);
            await sut.CommitAsync(batch.Id);

            await Assert.ThrowsAsync<ImportNotDryRunException>(() => sut.CommitAsync(batch.Id));
        }

        [Fact]
        [BusinessRule("BR-SYS-003")]
        public async Task Rollback_is_blocked_once_a_later_batch_has_committed_for_the_same_template()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            var first = await sut.StartDryRunAsync(1, "students-parents", rowCount: 100, errorCount: 0);
            await sut.CommitAsync(first.Id);
            _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
            var second = await sut.StartDryRunAsync(1, "students-parents", rowCount: 50, errorCount: 0);
            await sut.CommitAsync(second.Id);

            await Assert.ThrowsAsync<ImportRollbackWindowClosedException>(() => sut.RollbackAsync(first.Id));
        }

        // --- BR-SYS-005/BR-AUM-005 purge orchestration ----------------------------------

        [Fact]
        [BusinessRule("BR-SYS-005")]
        public async Task Executing_an_eligible_purge_dual_confirms_and_snapshots_first()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            var purge = await sut.RequestPurgeAsync(PurgeDataClass.Attachment, schoolId: 1, horizonUtc: _clock.UtcNow.AddDays(-1), requestedByUserId: 1);

            var executed = await sut.ApproveAndExecutePurgeAsync(purge.Id, secondApproverUserId: 2);

            Assert.Equal(PurgeExecutionStatus.Executed, executed.Status);
            Assert.NotNull(executed.CertificateNo);
            Assert.Single(db.SnapshotEvents.Where(s => s.TriggerOperation == "PurgeExecution"));
        }

        [Fact]
        [BusinessRule("BR-SYS-005")]
        public async Task Self_approval_is_rejected()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            var purge = await sut.RequestPurgeAsync(PurgeDataClass.Attachment, schoolId: 1, horizonUtc: _clock.UtcNow.AddDays(-1), requestedByUserId: 1);

            await Assert.ThrowsAsync<SelfApprovalNotAllowedException>(() => sut.ApproveAndExecutePurgeAsync(purge.Id, secondApproverUserId: 1));
        }

        [Fact]
        [BusinessRule("BR-SYS-005")]
        public async Task A_purge_before_its_horizon_is_not_eligible()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            var purge = await sut.RequestPurgeAsync(PurgeDataClass.Attachment, schoolId: 1, horizonUtc: _clock.UtcNow.AddDays(10), requestedByUserId: 1);

            await Assert.ThrowsAsync<PurgeNotEligibleException>(() => sut.ApproveAndExecutePurgeAsync(purge.Id, secondApproverUserId: 2));
            Assert.Equal(PurgeExecutionStatus.Blocked, db.PurgeExecutions.Single(p => p.Id == purge.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-SYS-005")]
        public async Task A_purge_under_an_active_legal_hold_is_blocked()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);
            await sut.PlaceLegalHoldAsync(PurgeDataClass.Attachment, "Attachment#42", placedByUserId: 9);
            var purge = await sut.RequestPurgeAsync(PurgeDataClass.Attachment, schoolId: 1, horizonUtc: _clock.UtcNow.AddDays(-1), requestedByUserId: 1);

            await Assert.ThrowsAsync<PurgeNotEligibleException>(() => sut.ApproveAndExecutePurgeAsync(purge.Id, secondApproverUserId: 2));
        }

        [Fact]
        [BusinessRule("BR-AUM-005")]
        public async Task Audit_data_purge_is_blocked_while_a_verification_failure_is_unresolved()
        {
            using var db = CreateContext();
            var checkpointService = new IntegrityCheckpointService(db, _clock);
            var auditAdmin = new AuditAdmin(db, _clock, checkpointService);
            var checkpoint = await checkpointService.ComputeAsync(_clock.UtcNow.AddHours(-1), _clock.UtcNow.AddHours(1));
            db.AuditEntries.Add(new Sms.Domain.Audit.AuditEntry
            {
                EntityType = "Student", EntityId = 1, Action = Sms.Domain.Audit.AuditAction.Update, ActorUserId = 1,
                CorrelationId = Guid.NewGuid(), OccurredAtUtc = _clock.UtcNow,
            });
            await db.SaveChangesAsync();
            await auditAdmin.RunIntegrityVerificationAsync(checkpoint.Id);
            var sut = new SysAdminService(db, _clock, new BackupAdmin(db, _clock), auditAdmin);
            var purge = await sut.RequestPurgeAsync(PurgeDataClass.Audit, schoolId: null, horizonUtc: _clock.UtcNow.AddDays(-1), requestedByUserId: 1);

            await Assert.ThrowsAsync<PurgeNotEligibleException>(() => sut.ApproveAndExecutePurgeAsync(purge.Id, secondApproverUserId: 2));
        }

        // --- BR-SYS-008 diagnostics ------------------------------------------------------

        [Fact]
        [BusinessRule("BR-SYS-008")]
        public async Task Generating_a_diagnostics_bundle_records_who_and_when()
        {
            using var db = CreateContext();
            var sut = CreateSut(db);

            var bundle = await sut.GenerateDiagnosticsBundleAsync(schoolId: 1, generatedByUserId: 7);

            Assert.NotEmpty(bundle.Reference);
        }
    }
}
