using Sms.Domain.Common;

namespace Sms.Domain.Schools
{
    /// <summary>
    /// core.SchoolGroup (BR-SCH-007): exists from v1 as an optional parent
    /// reference so multi-school later needs no schema change; no
    /// consolidation logic in v1 — v1 UI shows it only when more than one
    /// School exists.
    /// </summary>
    public class SchoolGroup : AuditableEntity
    {
        public LocalizedName Name { get; set; } = new();

        public bool IsActive { get; set; } = true;
    }
}
