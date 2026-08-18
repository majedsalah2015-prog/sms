using System;
using Sms.Domain.Common;

namespace Sms.Domain.Backup
{
    /// <summary>
    /// ops.BackupRun (BR-BAK-001/006): one execution's component coverage
    /// and outcome. Complete only when database, attachment store, and
    /// configuration are all present — BackupCompletenessEvaluator is the
    /// single source of Status, never set ad hoc by a caller.
    /// </summary>
    public class BackupRun : AuditableEntity
    {
        public int BackupPolicyId { get; set; }

        public bool DatabaseIncluded { get; set; }

        public bool AttachmentStoreIncluded { get; set; }

        public bool ConfigurationIncluded { get; set; }

        public BackupRunStatus Status { get; set; }

        public long SizeBytes { get; set; }

        public DateTime RanAtUtc { get; set; }
    }
}
