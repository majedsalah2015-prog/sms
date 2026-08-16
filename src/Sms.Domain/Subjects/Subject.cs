using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Subjects
{
    /// <summary>
    /// core.Subject (doc/Modules/07 §7, BR-SUB-001): school-catalog entry.
    /// Catalog editing is T3 per doc §4 ("direct entry, audited T3").
    /// </summary>
    [Audited(AuditTier.T3)]
    public class Subject : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        /// <summary>Free-text code, not a closed enum — doc's list (core/language/religious/arts/PE…) is open-ended.</summary>
        public string Category { get; set; } = string.Empty;

        public int? DepartmentId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
