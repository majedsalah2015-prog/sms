using System;

namespace Sms.Domain.Common
{
    /// <summary>
    /// Base for all persisted entities. Carries the standard column set of
    /// BR-GLB-007 (docs/Database/01 §4); stamps are applied centrally by the
    /// DbContext, never by feature code.
    /// </summary>
    public abstract class AuditableEntity
    {
        public int Id { get; set; }

        public int CreatedByUserId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public int? ModifiedByUserId { get; set; }

        public DateTime? ModifiedAtUtc { get; set; }
    }
}
