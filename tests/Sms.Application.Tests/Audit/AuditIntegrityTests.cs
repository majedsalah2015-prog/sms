using System;
using System.Collections.Generic;
using Sms.Application.Audit;
using Sms.Domain.Audit;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Audit
{
    public class AuditIntegrityTests
    {
        private static AuditEntry Entry(long id, string? newValue = "85")
        {
            return new AuditEntry
            {
                Id = id,
                EntityType = "SensitiveRecord",
                EntityId = 7,
                FieldName = "Mark",
                OldValue = "78",
                NewValue = newValue,
                ActorUserId = 42,
                Action = AuditAction.Update,
                CorrelationId = new Guid("11111111-2222-3333-4444-555555555555"),
                OccurredAtUtc = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc),
            };
        }

        [Fact]
        [BusinessRule("BR-AUD-007")]
        public void Entries_hash_is_deterministic_and_value_sensitive()
        {
            var same1 = AuditIntegrity.ComputeEntriesHash(new List<AuditEntry> { Entry(1), Entry(2) });
            var same2 = AuditIntegrity.ComputeEntriesHash(new List<AuditEntry> { Entry(1), Entry(2) });
            var edited = AuditIntegrity.ComputeEntriesHash(new List<AuditEntry> { Entry(1), Entry(2, newValue: "99") });
            var missing = AuditIntegrity.ComputeEntriesHash(new List<AuditEntry> { Entry(1) });

            Assert.Equal(same1, same2);
            Assert.NotEqual(same1, edited);
            Assert.NotEqual(same1, missing);
        }

        [Fact]
        [BusinessRule("BR-AUD-007")]
        public void Verify_detects_edited_entries_and_broken_chains()
        {
            var entries = new List<AuditEntry> { Entry(1), Entry(2) };
            var entriesHash = AuditIntegrity.ComputeEntriesHash(entries);
            var checkpoint = new IntegrityCheckpoint
            {
                EntriesHash = entriesHash,
                PreviousChainHash = null,
                ChainHash = AuditIntegrity.ComputeChainHash(null, entriesHash),
            };

            Assert.True(AuditIntegrity.Verify(checkpoint, entries, previousChainHash: null));

            var tampered = new List<AuditEntry> { Entry(1), Entry(2, newValue: "99") };
            Assert.False(AuditIntegrity.Verify(checkpoint, tampered, previousChainHash: null));

            Assert.False(AuditIntegrity.Verify(checkpoint, entries, previousChainHash: "not-the-real-predecessor"));
        }

        [Fact]
        [BusinessRule("BR-AUD-007")]
        public void Canonical_form_cannot_collide_across_field_boundaries()
        {
            var a = Entry(1);
            a.OldValue = "78|";
            a.NewValue = "85";

            var b = Entry(1);
            b.OldValue = "78";
            b.NewValue = "|85";

            Assert.NotEqual(AuditIntegrity.CanonicalString(a), AuditIntegrity.CanonicalString(b));
        }
    }
}
