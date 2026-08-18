using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S7/E-704 (Audit admin, doc/Modules/34, BR-AUM-001/002) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class AuditAdminTests : IDisposable
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

        public AuditAdminTests()
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

        // --- BR-AUM-002 anomaly rule/hit disposition ---------------------------------------

        [Fact]
        [BusinessRule("BR-AUM-002")]
        public async Task Disposing_an_open_hit_records_the_note_and_actor()
        {
            using var db = CreateContext();
            var admin = new AuditAdmin(db, _clock, new IntegrityCheckpointService(db, _clock));
            var rule = await admin.DefineAnomalyRuleAsync("OUT_OF_HOURS", "خارج الدوام", "Out of hours", AnomalySeverity.Medium);
            var hit = await admin.RecordAnomalyHitAsync(rule.Id, auditEntryId: 1, contextJson: "{}");

            var disposed = await admin.DispositionAnomalyHitAsync(hit.Id, AnomalyHitStatus.Dismissed, dispositionedByUserId: 5, "Reviewed, false positive");

            Assert.Equal(AnomalyHitStatus.Dismissed, disposed.Status);
            Assert.Equal(5, disposed.DispositionedByUserId);
            Assert.NotNull(disposed.DispositionedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-AUM-002")]
        public async Task Disposing_an_already_dispositioned_hit_is_rejected()
        {
            using var db = CreateContext();
            var admin = new AuditAdmin(db, _clock, new IntegrityCheckpointService(db, _clock));
            var rule = await admin.DefineAnomalyRuleAsync("OUT_OF_HOURS", "خارج الدوام", "Out of hours", AnomalySeverity.Medium);
            var hit = await admin.RecordAnomalyHitAsync(rule.Id, auditEntryId: 1, contextJson: "{}");
            await admin.DispositionAnomalyHitAsync(hit.Id, AnomalyHitStatus.Dismissed, 5, "Reviewed");

            await Assert.ThrowsAsync<AnomalyHitAlreadyDispositionedException>(() =>
                admin.DispositionAnomalyHitAsync(hit.Id, AnomalyHitStatus.Escalated, 5, "Second look"));
        }

        // --- BR-AUM-001 integrity verification / purge freeze ------------------------------

        [Fact]
        [BusinessRule("BR-AUM-001")]
        public async Task Verifying_an_untampered_checkpoint_passes()
        {
            using var db = CreateContext();
            var checkpointService = new IntegrityCheckpointService(db, _clock);
            db.AuditEntries.Add(new AuditEntry
            {
                EntityType = "Student", EntityId = 1, Action = AuditAction.Update, ActorUserId = 1,
                CorrelationId = Guid.NewGuid(), OccurredAtUtc = _clock.UtcNow,
            });
            await db.SaveChangesAsync();
            var checkpoint = await checkpointService.ComputeAsync(_clock.UtcNow.AddHours(-1), _clock.UtcNow.AddHours(1));
            var admin = new AuditAdmin(db, _clock, checkpointService);

            var run = await admin.RunIntegrityVerificationAsync(checkpoint.Id);

            Assert.True(run.Passed);
            Assert.True(run.IsResolved);
            Assert.False(await admin.IsAuditPurgeFrozenAsync());
        }

        [Fact]
        [BusinessRule("BR-AUM-001")]
        public async Task A_backdated_entry_after_checkpointing_fails_verification_and_freezes_purge()
        {
            using var db = CreateContext();
            var checkpointService = new IntegrityCheckpointService(db, _clock);
            var periodStart = _clock.UtcNow.AddHours(-1);
            var periodEnd = _clock.UtcNow.AddHours(1);
            var checkpoint = await checkpointService.ComputeAsync(periodStart, periodEnd);

            // Simulates storage-level tampering: a row landing inside an
            // already-checkpointed period after the checkpoint was computed.
            db.AuditEntries.Add(new AuditEntry
            {
                EntityType = "Student", EntityId = 1, Action = AuditAction.Update, ActorUserId = 1,
                CorrelationId = Guid.NewGuid(), OccurredAtUtc = _clock.UtcNow,
            });
            await db.SaveChangesAsync();
            var admin = new AuditAdmin(db, _clock, checkpointService);

            var run = await admin.RunIntegrityVerificationAsync(checkpoint.Id);

            Assert.False(run.Passed);
            Assert.False(run.IsResolved);
            Assert.True(await admin.IsAuditPurgeFrozenAsync());

            await admin.ResolveVerificationFailureAsync(run.Id);
            Assert.False(await admin.IsAuditPurgeFrozenAsync());
        }
    }
}
