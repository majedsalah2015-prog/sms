using System;
using Sms.Domain.Common;

namespace Sms.Domain.Backup
{
    /// <summary>
    /// ops.SnapshotEvent (BR-BAK-004): a labeled pre-operation snapshot
    /// taken automatically before rollover activation, purges, upgrades, and
    /// bulk imports. The initiating operation blocks on Success == false —
    /// enforced by the caller (PurgeAdmin/ImportAdmin), not by this record.
    /// </summary>
    public class SnapshotEvent : AuditableEntity
    {
        public string Label { get; set; } = string.Empty;

        public string TriggerOperation { get; set; } = string.Empty;

        public bool Success { get; set; }

        public DateTime TakenAtUtc { get; set; }
    }
}
