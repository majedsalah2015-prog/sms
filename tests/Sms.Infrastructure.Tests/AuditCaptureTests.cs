using System;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Infrastructure.Audit;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>E-004: T1–T3 write capture per doc 07 §3–4, enforced centrally in the context.</summary>
    public sealed class AuditCaptureTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 42;
        }

        private sealed class FixedTenant : ITenantContext
        {
            public FixedTenant(int schoolId)
            {
                SchoolId = schoolId;
            }

            public int SchoolId { get; }
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly AuditContext _audit = new() { SourceScreen = "/grading/marks", ClientIp = "10.0.0.5" };

        public AuditCaptureTests()
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

        private TestDbContext CreateContext(int schoolId = 1)
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new TestDbContext(options, new FixedTenant(schoolId), _user, _clock, _audit);
        }

        private int SeedSensitive(decimal mark = 78m)
        {
            using var db = CreateContext();
            var record = new SensitiveRecord { StudentNo = "S-1001", Mark = mark };
            db.SensitiveRecords.Add(record);
            db.SaveChanges();
            return record.Id;
        }

        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void T1_field_change_writes_old_new_actor_and_reason()
        {
            var id = SeedSensitive(mark: 78.5m);

            using (var db = CreateContext())
            {
                _audit.Reason = "Remark approved on appeal";
                db.SensitiveRecords.Single(r => r.Id == id).Mark = 85m;
                db.SaveChanges();
                _audit.Reason = null;
            }

            using (var db = CreateContext())
            {
                var entry = db.AuditEntries.Single(e => e.FieldName == "Mark");
                Assert.Equal(nameof(SensitiveRecord), entry.EntityType);
                Assert.Equal(id, entry.EntityId);
                Assert.Equal("S-1001", entry.BusinessKey);
                Assert.Equal("78.5", entry.OldValue);
                Assert.Equal("85", entry.NewValue);
                Assert.Equal(AuditAction.Update, entry.Action);
                Assert.Equal(42, entry.ActorUserId);
                Assert.Equal("Remark approved on appeal", entry.Reason);
                Assert.Equal("/grading/marks", entry.SourceScreen);
                Assert.Equal("10.0.0.5", entry.ClientIp);
                Assert.Equal(1, entry.SchoolId);
                Assert.Equal(_clock.UtcNow, entry.OccurredAtUtc);
            }
        }

        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void T1_reason_required_field_without_reason_fails_the_save()
        {
            var id = SeedSensitive();

            using (var db = CreateContext())
            {
                _audit.Reason = null;
                db.SensitiveRecords.Single(r => r.Id == id).Mark = 90m;

                Assert.Throws<MissingAuditReasonException>(() => db.SaveChanges());
            }

            using (var db = CreateContext())
            {
                Assert.Equal(78m, db.SensitiveRecords.Single(r => r.Id == id).Mark);
            }
        }

        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void T1_fields_without_reason_marker_change_freely()
        {
            var id = SeedSensitive();

            using (var db = CreateContext())
            {
                _audit.Reason = null;
                db.SensitiveRecords.Single(r => r.Id == id).Note = "Transferred from section B";
                db.SaveChanges();
            }

            using var check = CreateContext();
            var entry = check.AuditEntries.Single(e => e.FieldName == "Note");
            Assert.Null(entry.OldValue);
            Assert.Equal("Transferred from section B", entry.NewValue);
            Assert.Null(entry.Reason);
        }

        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void T2_field_change_logs_old_new_with_optional_reason()
        {
            int id;
            using (var db = CreateContext())
            {
                var record = new StandardRecord { Phone = "0501111111" };
                db.StandardRecords.Add(record);
                db.SaveChanges();
                id = record.Id;
            }

            using (var db = CreateContext())
            {
                _audit.Reason = null;
                db.StandardRecords.Single(r => r.Id == id).Phone = "0502222222";
                db.SaveChanges();
            }

            using var check = CreateContext();
            var entry = check.AuditEntries.Single(e => e.EntityType == nameof(StandardRecord) && e.FieldName == "Phone");
            Assert.Equal("0501111111", entry.OldValue);
            Assert.Equal("0502222222", entry.NewValue);
            Assert.Null(entry.Reason);
        }

        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void T3_logs_record_level_events_without_field_diffs()
        {
            int id;
            using (var db = CreateContext())
            {
                var item = new MasterItem { Name = "Bus route A" };
                db.MasterItems.Add(item);
                db.SaveChanges();
                id = item.Id;
            }

            using (var db = CreateContext())
            {
                db.MasterItems.Single(m => m.Id == id).Name = "Bus route A1";
                db.SaveChanges();

                db.MasterItems.Single(m => m.Id == id).IsActive = false;
                db.SaveChanges();
            }

            using var check = CreateContext();
            var entries = check.AuditEntries
                .Where(e => e.EntityType == nameof(MasterItem) && e.EntityId == id)
                .OrderBy(e => e.Id)
                .ToList();

            Assert.Equal(3, entries.Count);
            Assert.All(entries, e => Assert.Null(e.FieldName));
            Assert.Equal(AuditAction.Create, entries[0].Action);
            Assert.Equal(AuditAction.Update, entries[1].Action);
            Assert.Equal(AuditAction.StatusChange, entries[2].Action);
        }

        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void Create_entry_carries_the_generated_entity_id()
        {
            var id = SeedSensitive();

            using var check = CreateContext();
            var entry = check.AuditEntries.Single(e => e.EntityType == nameof(SensitiveRecord) && e.Action == AuditAction.Create);
            Assert.Equal(id, entry.EntityId);
            Assert.Equal("S-1001", entry.BusinessKey);
        }

        [Fact]
        public void Entries_of_one_save_share_a_correlation_id()
        {
            var id = SeedSensitive();

            using (var db = CreateContext())
            {
                _audit.Reason = "Data-entry correction";
                var record = db.SensitiveRecords.Single(r => r.Id == id);
                record.Mark = 80m;
                record.Note = "Corrected";
                db.SaveChanges();
                _audit.Reason = null;
            }

            using var check = CreateContext();
            var updates = check.AuditEntries
                .Where(e => e.EntityId == id && e.Action == AuditAction.Update)
                .ToList();
            var create = check.AuditEntries.Single(e => e.EntityId == id && e.Action == AuditAction.Create);

            Assert.Equal(2, updates.Count);
            Assert.Single(updates.Select(e => e.CorrelationId).Distinct());
            Assert.NotEqual(create.CorrelationId, updates[0].CorrelationId);
        }

        [Fact]
        [BusinessRule("BR-AUD-001")]
        public void Audit_entries_can_never_be_updated_or_deleted()
        {
            SeedSensitive();

            using (var db = CreateContext())
            {
                var entry = db.AuditEntries.First();
                entry.NewValue = "tampered";

                Assert.Throws<AuditImmutableException>(() => db.SaveChanges());
            }

            using (var db = CreateContext())
            {
                var entry = db.AuditEntries.First();
                db.AuditEntries.Remove(entry);

                Assert.Throws<AuditImmutableException>(() => db.SaveChanges());
            }
        }

        [Fact]
        [BusinessRule("BR-AUD-003")]
        public void Failed_business_save_leaves_no_partial_audit_entries()
        {
            var id = SeedSensitive();
            int before;
            using (var db = CreateContext())
            {
                before = db.AuditEntries.Count();
            }

            using (var db = CreateContext())
            {
                _audit.Reason = "Doomed save";
                db.SensitiveRecords.Single(r => r.Id == id).Mark = 95m;
                db.SensitiveRecords.Add(new SensitiveRecord { StudentNo = null! });

                Assert.Throws<DbUpdateException>(() => db.SaveChanges());
                _audit.Reason = null;
            }

            using (var db = CreateContext())
            {
                Assert.Equal(before, db.AuditEntries.Count());
                Assert.Equal(78m, db.SensitiveRecords.Single(r => r.Id == id).Mark);
            }
        }

        [Fact]
        [BusinessRule("BR-AUD-005")]
        public void Values_are_stored_raw_and_culture_invariant()
        {
            var id = SeedSensitive();

            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                using var db = CreateContext();
                _audit.Reason = "Fraction check";
                db.SensitiveRecords.Single(r => r.Id == id).Mark = 85.5m;
                db.SaveChanges();
                _audit.Reason = null;
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }

            using var check = CreateContext();
            var entry = check.AuditEntries.Single(e => e.FieldName == "Mark");
            Assert.Equal("85.5", entry.NewValue);
        }
    }
}
