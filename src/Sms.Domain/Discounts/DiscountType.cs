using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discounts
{
    /// <summary>
    /// ppl.DiscountType (doc/Modules/22 §7, BR-DIS-001): policy fields,
    /// stacking, renewal mode. Applicable category is a single optional
    /// FeeCategory (null = every category) — same stand-in for the doc's
    /// "applicable categories" as E-501's PlanTemplate. Caps per family
    /// (vs. per student) need a family entity Fees doesn't have — only
    /// the per-student cap is modeled.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class DiscountType : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public DiscountBasis Basis { get; set; } = DiscountBasis.Percentage;

        public DiscountComputationStage ComputationStage { get; set; } = DiscountComputationStage.BeforeVat;

        /// <summary>Null = all categories.</summary>
        public int? FeeCategoryId { get; set; }

        public decimal? CapAmountPerStudent { get; set; }

        /// <summary>BR-DIS-001: non-stackable types can't combine with any other grant on the same category/year.</summary>
        public bool IsStackable { get; set; } = true;

        /// <summary>BR-DIS-001: max combined percentage across stacked grants — global cap default 100% guarded.</summary>
        public decimal MaxCombinedPercent { get; set; } = 100m;

        public DiscountEligibilityMode EligibilityMode { get; set; } = DiscountEligibilityMode.Manual;

        public DiscountRenewalMode RenewalMode { get; set; } = DiscountRenewalMode.ManualRegrant;

        /// <summary>BR-DIS-003: hardship types require restricted documentation (BR-GLB-072) — attachment linkage deferred; the flag drives the doc-required validation.</summary>
        public bool RequiresHardshipDocumentation { get; set; }

        public bool IsActive { get; set; } = true;

        public List<EligibilityRule> EligibilityRules { get; set; } = new();
    }
}
