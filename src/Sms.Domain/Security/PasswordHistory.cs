using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.PasswordHistory. One row per password ever set, oldest-first;
    /// BR-SEC-001 checks the last N (product default 5) to block reuse.
    /// Append-only in practice — rows are never edited, only pruned.
    /// </summary>
    public class PasswordHistory : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int UserAccountId { get; set; }

        public string PasswordHash { get; set; } = string.Empty;
    }
}
