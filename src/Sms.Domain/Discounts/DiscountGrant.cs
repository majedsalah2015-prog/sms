using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Discounts
{
    /// <summary>
    /// ppl.DiscountGrant (doc/Modules/22 §7, BR-DIS-003/004/008/009): the
    /// register row — student-year × type, basis value, source, workflow
    /// state, who approved, revocation. T1 with reasons; RevokedEffectiveDate
    /// is reason-required (it only ever flips on an existing row, so
    /// RequiresAuditReason fires exactly on revocation — BR-DIS-008's
    /// "with reason" — never on the initial grant).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class DiscountGrant : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int StudentId { get; set; }

        public int DiscountTypeId { get; set; }

        public DiscountGrantSource Source { get; set; }

        /// <summary>Percentage or fixed amount per DiscountType.Basis.</summary>
        public decimal BasisValue { get; set; }

        public DiscountGrantStatus Status { get; set; } = DiscountGrantStatus.Proposed;

        /// <summary>BR-DIS-003 threshold-routed tier (or Committee for scholarships) — recorded for the register, chain not routed.</summary>
        public ApprovalTier RequiredTier { get; set; }

        public string Reason { get; set; } = string.Empty;

        public int ProposedByUserId { get; set; }

        public int? ApprovedByUserId { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        /// <summary>BR-DIS-004: set for scholarship awards; envelope consumption counts these.</summary>
        public int? ScholarshipProgramId { get; set; }

        public string? SponsorNote { get; set; }

        /// <summary>BR-DIS-004: Owner-level override of an exhausted envelope (T1 + reason).</summary>
        public string? EnvelopeOverrideReason { get; set; }

        /// <summary>BR-DIS-005: total actually applied as discount documents (after never-negative capping) — what the register reports as "got".</summary>
        public decimal AppliedAmount { get; set; }

        [RequiresAuditReason]
        public DateTime? RevokedEffectiveDate { get; set; }

        public string? RevokedReason { get; set; }

        /// <summary>BR-DIS-007: the prior-year grant this one renews, when Source = Renewal.</summary>
        public int? RenewedFromGrantId { get; set; }
    }
}
