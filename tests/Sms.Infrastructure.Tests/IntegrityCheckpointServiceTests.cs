using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Infrastructure.Audit;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>E-004 tamper-evidence: hash-chained checkpoints over the audit store.</summary>
    public sealed class IntegrityCheckpointServiceTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId => 42;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public IntegrityCheckpointServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var setup = CreateContext();
            setup.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private TestDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new TestDbContext(options, _tenant, new FixedUser(), _clock, _audit);
        }

        private void SeedAuditedChanges()
        {
            using var db = CreateContext();
            db.StandardRecords.Add(new StandardRecord { Phone = "0501111111" });
            db.MasterItems.Add(new MasterItem { Name = "Route A" });
            db.SaveChanges();
        }

        private static (DateTime Start, DateTime End) Day(DateTime utc)
        {
            var start = utc.Date;
            return (start, start.AddDays(1));
        }

        [Fact]
        [BusinessRule("BR-AUD-007")]
        public async Task Checkpoint_over_untouched_entries_verifies()
        {
            SeedAuditedChanges();

            using var db = CreateContext();
            var service = new IntegrityCheckpointService(db, _clock);
            var (start, end) = Day(_clock.UtcNow);

            var checkpoint = await service.ComputeAsync(start, end);

            Assert.True(checkpoint.EntryCount > 0);
            Assert.True(await service.VerifyAsync(checkpoint.Id));
        }

        [Fact]
        [BusinessRule("BR-AUD-007")]
        public async Task Storage_level_edit_is_detected()
        {
            SeedAuditedChanges();

            using var db = CreateContext();
            var service = new IntegrityCheckpointService(db, _clock);
            var (start, end) = Day(_clock.UtcNow);
            var checkpoint = await service.ComputeAsync(start, end);

            // Simulate a DBA-level edit that bypasses the application guard.
            var victimId = db.AuditEntries.OrderBy(e => e.Id).First().Id;
            db.Database.ExecuteSqlRaw("UPDATE AuditEntry SET ActorUserId = 999 WHERE Id = {0}", victimId);

            Assert.False(await service.VerifyAsync(checkpoint.Id));
        }

        [Fact]
        [BusinessRule("BR-AUD-007")]
        public async Task Checkpoints_chain_to_their_predecessor()
        {
            SeedAuditedChanges();

            using var db = CreateContext();
            var service = new IntegrityCheckpointService(db, _clock);
            var (start, end) = Day(_clock.UtcNow);

            var first = await service.ComputeAsync(start, end);

            _clock.UtcNow = _clock.UtcNow.AddDays(1);
            using (var more = CreateContext())
            {
                more.MasterItems.Add(new MasterItem { Name = "Route B" });
                more.SaveChanges();
            }

            var (start2, end2) = Day(_clock.UtcNow);
            var second = await service.ComputeAsync(start2, end2);

            Assert.Null(first.PreviousChainHash);
            Assert.Equal(first.ChainHash, second.PreviousChainHash);
            Assert.True(await service.VerifyAsync(second.Id));
        }

        [Fact]
        public void Event_writer_logs_record_level_events_atomically_with_the_save()
        {
            using (var db = CreateContext())
            {
                var writer = new AuditEventWriter(db, _tenant, _tenant, new FixedUser(), _clock, _audit);
                _audit.SourceScreen = "/students/5/file";
                writer.Log(AuditAction.Export, "Student", entityId: 5, businessKey: "S-1001");
                db.SaveChanges();
            }

            using var check = CreateContext();
            var entry = check.AuditEntries.Single(e => e.Action == AuditAction.Export);
            Assert.Equal("Student", entry.EntityType);
            Assert.Equal(5, entry.EntityId);
            Assert.Equal("S-1001", entry.BusinessKey);
            Assert.Equal(1, entry.SchoolId);
            Assert.Equal(2027, entry.AcademicYearId);
            Assert.Equal(42, entry.ActorUserId);
            Assert.Equal("/students/5/file", entry.SourceScreen);
        }
    }
}
