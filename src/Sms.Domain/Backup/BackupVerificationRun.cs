using System;
using Sms.Domain.Common;

namespace Sms.Domain.Backup
{
    /// <summary>
    /// ops.BackupVerificationRun (BR-BAK-003, NF-A4): the scheduled test-
    /// restore checks. A backup generation is Trusted only after its
    /// BackupRun's last verification passed — BackupTrustEvaluator is the
    /// single source of that determination. Named distinctly from Module
    /// 34's IntegrityVerificationRun to avoid a same-name collision.
    /// </summary>
    public class BackupVerificationRun : AuditableEntity
    {
        public int BackupRunId { get; set; }

        public bool DatabaseRestoreOk { get; set; }

        public bool RowCountSanityOk { get; set; }

        public bool AttachmentHashSampleOk { get; set; }

        public bool IntegrityCheckpointOk { get; set; }

        public DateTime CheckedAtUtc { get; set; }
    }
}
