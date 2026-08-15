using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Lookups
{
    /// <summary>
    /// core.LookupCategory (BR-SET-001/007, doc/Modules/01 §7). The taxonomy
    /// itself — which categories exist and their tier — is product-defined;
    /// the values within a category are what schools may extend when the
    /// tier allows it.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class LookupCategory : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>e.g. "Nationality", "BloodType", "IdType".</summary>
        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public LookupCategoryTier Tier { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
