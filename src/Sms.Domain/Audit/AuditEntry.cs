using System;

namespace Sms.Domain.Audit
{
    /// <summary>
    /// One append-only audit record per doc 07 §4 / DB spec A12. Deliberately
    /// NOT an <see cref="Sms.Domain.Common.AuditableEntity"/> — the audit store
    /// has its own shape (BIGINT id, no modify stamps: nothing may modify it,
    /// BR-AUD-001) and is not tenant-filtered (auditors search cross-school
    /// under their own permission gate, BR-AUD-004).
    /// </summary>
    public class AuditEntry
    {
        public long Id { get; set; }

        public int? SchoolId { get; set; }

        public int? AcademicYearId { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public long? EntityId { get; set; }

        /// <summary>Human-readable key that survives deactivation (doc 07 §4).</summary>
        public string? BusinessKey { get; set; }

        /// <summary>Null = record-level event (T3 or non-data domains).</summary>
        public string? FieldName { get; set; }

        /// <summary>Raw invariant value — single stored truth, display layer localizes (BR-AUD-005).</summary>
        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        public int ActorUserId { get; set; }

        /// <summary>Role in effect; populated once session context lands (E-003 remaining slices).</summary>
        public int? ActingRoleId { get; set; }

        public bool IsDelegated { get; set; }

        public AuditAction Action { get; set; }

        public string? Reason { get; set; }

        /// <summary>Groups one save's changes (doc 07 §4).</summary>
        public Guid CorrelationId { get; set; }

        /// <summary>Screen / import / job / API origin.</summary>
        public string? SourceScreen { get; set; }

        public string? ClientIp { get; set; }

        public DateTime OccurredAtUtc { get; set; }
    }
}
