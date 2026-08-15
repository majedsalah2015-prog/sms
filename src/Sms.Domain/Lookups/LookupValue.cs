using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Lookups
{
    /// <summary>
    /// core.LookupValue (BR-SET-001/002/007). BR-SET-002: a value referenced
    /// anywhere is deactivatable, never deletable — enforced for free by the
    /// existing IActivatable hard-delete guard on SmsDbContext, no extra
    /// code needed here.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class LookupValue : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int LookupCategoryId { get; set; }

        /// <summary>Stable machine key (e.g. "SA"), never re-purposed once referenced.</summary>
        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
