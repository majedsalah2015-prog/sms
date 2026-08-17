using Sms.Domain.Common;

namespace Sms.Domain.Discounts
{
    /// <summary>
    /// ppl.EligibilityRule (doc/Modules/22 §7, BR-DIS-002): one ladder
    /// step of an automatic type. SiblingLadder rows say "the Nth child
    /// and up gets X%" (ChildOrdinal = N); a Staff row says "an active
    /// employee's child gets X%" (ChildOrdinal null). Child of
    /// DiscountType — carries its own SchoolId for the tenant filter.
    /// </summary>
    public class EligibilityRule : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int DiscountTypeId { get; set; }

        public EligibilityRuleKind Kind { get; set; }

        /// <summary>SiblingLadder: applies from this child ordinal (1 = eldest enrolled) upward until a higher step takes over.</summary>
        public int? ChildOrdinal { get; set; }

        public decimal Percent { get; set; }
    }
}
