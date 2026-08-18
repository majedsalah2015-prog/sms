using System;
using Sms.Domain.Common;

namespace Sms.Domain.SysAdmin
{
    /// <summary>ops.LegalHold: blocks purge of a data class/subject pending legal resolution (BR-SYS-005's "respects legal holds"). Active while ReleasedAtUtc is null.</summary>
    public class LegalHold : AuditableEntity
    {
        public PurgeDataClass DataClass { get; set; }

        public string SubjectReference { get; set; } = string.Empty;

        public int PlacedByUserId { get; set; }

        public DateTime PlacedAtUtc { get; set; }

        public DateTime? ReleasedAtUtc { get; set; }
    }
}
