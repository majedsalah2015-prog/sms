using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Backup;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Backup;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S7/E-704 (Backup, doc/Modules/35, BR-BAK-001/003/004/005) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class BackupAdminTests : IDisposable
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

        public BackupAdminTests()
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

        // --- BR-BAK-001 completeness --------------------------------------------------------

        [Fact]
        [BusinessRule("BR-BAK-001")]
        public async Task A_run_covering_all_three_components_is_complete()
        {
            using var db = CreateContext();
            var admin = new BackupAdmin(db, _clock);
            var policy = await admin.DefinePolicyAsync(BackupDeploymentClass.Cloud, 30, 12, 7, onPremResponsibilityAcknowledged: false);

            var run = await admin.RecordRunAsync(policy.Id, databaseIncluded: true, attachmentStoreIncluded: true, configurationIncluded: true, sizeBytes: 1024);

            Assert.Equal(BackupRunStatus.Complete, run.Status);
        }

        [Fact]
        [BusinessRule("BR-BAK-001")]
        public async Task A_partial_run_is_degraded()
        {
            using var db = CreateContext();
            var admin = new BackupAdmin(db, _clock);
            var policy = await admin.DefinePolicyAsync(BackupDeploymentClass.Cloud, 30, 12, 7, onPremResponsibilityAcknowledged: false);

            var run = await admin.RecordRunAsync(policy.Id, databaseIncluded: true, attachmentStoreIncluded: false, configurationIncluded: true, sizeBytes: 1024);

            Assert.Equal(BackupRunStatus.Degraded, run.Status);
        }

        // --- BR-BAK-004 pre-op snapshot ------------------------------------------------------

        [Fact]
        [BusinessRule("BR-BAK-004")]
        public async Task A_successful_snapshot_is_recorded()
        {
            using var db = CreateContext();
            var admin = new BackupAdmin(db, _clock);

            var snapshot = await admin.TakeSnapshotAsync("pre-import-1", "ImportCommit", snapshotSucceeded: true);

            Assert.True(snapshot.Success);
        }

        [Fact]
        [BusinessRule("BR-BAK-004")]
        public async Task A_failed_snapshot_is_recorded_and_throws()
        {
            using var db = CreateContext();
            var admin = new BackupAdmin(db, _clock);

            await Assert.ThrowsAsync<SnapshotFailedException>(() => admin.TakeSnapshotAsync("pre-purge-1", "PurgeExecution", snapshotSucceeded: false));

            var recorded = db.SnapshotEvents.Single();
            Assert.False(recorded.Success);
        }

        // --- BR-BAK-005 restore case chain ---------------------------------------------------

        [Fact]
        [BusinessRule("BR-BAK-005")]
        public async Task Advancing_the_restore_case_through_the_legal_chain_succeeds()
        {
            using var db = CreateContext();
            var admin = new BackupAdmin(db, _clock);
            var restoreCase = await admin.RequestRestoreAsync(requestedByUserId: 1, RestoreScope.Tenant, schoolId: 1, pointInTimeUtc: _clock.UtcNow.AddDays(-1));

            await admin.AdvanceRestoreCaseAsync(restoreCase.Id, RestoreCaseStatus.ScopeDefined);
            await admin.AdvanceRestoreCaseAsync(restoreCase.Id, RestoreCaseStatus.Executed);
            var verified = await admin.AdvanceRestoreCaseAsync(restoreCase.Id, RestoreCaseStatus.Verified, gapAnalysisNote: "3 transactions since restore point");

            Assert.Equal(RestoreCaseStatus.Verified, verified.Status);
            Assert.Equal("3 transactions since restore point", verified.GapAnalysisNote);
        }

        [Fact]
        [BusinessRule("BR-BAK-005")]
        public async Task Skipping_a_step_in_the_restore_chain_is_rejected()
        {
            using var db = CreateContext();
            var admin = new BackupAdmin(db, _clock);
            var restoreCase = await admin.RequestRestoreAsync(requestedByUserId: 1, RestoreScope.Full, schoolId: null, pointInTimeUtc: _clock.UtcNow.AddDays(-1));

            await Assert.ThrowsAsync<InvalidRestoreCaseTransitionException>(() =>
                admin.AdvanceRestoreCaseAsync(restoreCase.Id, RestoreCaseStatus.Executed));
        }
    }
}
