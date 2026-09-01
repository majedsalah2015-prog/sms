using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Discounts;

namespace Sms.Application.Discounts
{
    /// <summary>BR-DIS-002 ladder/staff rule input.</summary>
    public sealed record EligibilityRuleInput(EligibilityRuleKind Kind, decimal Percent, int? ChildOrdinal = null);

    /// <summary>
    /// doc/Modules/22 §8 Type catalog / Grant desk / Scholarship board /
    /// Renewal queue / Waiver desk screens backing (screens deferred, the
    /// operations are core). Every approval chain here is recorded as an
    /// ApprovalTier on the row (status-only workflow substitution, same
    /// as every other WF in this build) — the routing decision is real,
    /// the inbox routing is not.
    /// </summary>
    public interface IDiscountAdmin
    {
        Task<DiscountType> DefineTypeAsync(
            string nameAr, string nameEn, DiscountBasis basis, DiscountEligibilityMode eligibilityMode,
            int? feeCategoryId = null, DiscountComputationStage stage = DiscountComputationStage.BeforeVat, decimal? capAmountPerStudent = null,
            bool isStackable = true, decimal maxCombinedPercent = 100m, DiscountRenewalMode renewalMode = DiscountRenewalMode.ManualRegrant,
            bool requiresHardshipDocumentation = false, IReadOnlyList<EligibilityRuleInput>? rules = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// doc/Modules/22 §8.2. Corrects a type in place. The catalog could only append: a
        /// mistyped name, a wrong basis or a stacking cap set too high could never be fixed,
        /// only shadowed by a second type — and the register then carried both.
        /// <para>
        /// The eligibility rules are replaced wholesale rather than merged: BR-DIS-002 reads the
        /// ladder as one ordered set, and a partial update would leave a rung nobody entered.
        /// Grants already approved keep the terms they were granted under — this changes what the
        /// type means for the next grant, never what a past one paid.
        /// </para>
        /// </summary>
        Task UpdateTypeAsync(
            int discountTypeId, string nameAr, string nameEn, DiscountBasis basis, DiscountEligibilityMode eligibilityMode,
            int? feeCategoryId = null, DiscountComputationStage stage = DiscountComputationStage.BeforeVat, decimal? capAmountPerStudent = null,
            bool isStackable = true, decimal maxCombinedPercent = 100m, DiscountRenewalMode renewalMode = DiscountRenewalMode.ManualRegrant,
            bool requiresHardshipDocumentation = false, IReadOnlyList<EligibilityRuleInput>? rules = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-GLB-005: retires a type or puts it back. A retired type stops being offered to new
        /// grants; the grants already carrying it are untouched and keep renewing under it, which
        /// is why this is a flag and not a delete.
        /// </summary>
        Task SetTypeActiveAsync(int discountTypeId, bool isActive, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-DIS-003: manual grant, threshold-routed; stacking checked at grant (BR-DIS-001,
        /// <see cref="Common.Exceptions.DiscountStackingViolationException"/>); hardship types need
        /// <paramref name="hasHardshipDocumentation"/> (<see cref="Common.Exceptions.HardshipDocumentationRequiredException"/>).
        /// </summary>
        Task<DiscountGrant> ProposeManualGrantAsync(
            int studentId, int discountTypeId, decimal basisValue, string reason, int proposedByUserId,
            bool hasHardshipDocumentation = false, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-002: evaluates the type's automatic rules over the working year (sibling ladder via StudentGuardianLink families; staff via Parent↔Employee UserAccountId bridge) and creates one Proposed grant per eligible student — batch-approved with <see cref="ApproveGrantsAsync"/>.</summary>
        Task<IReadOnlyList<DiscountGrant>> ProposeAutomaticGrantsAsync(int discountTypeId, int proposedByUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-004: named program with a budget envelope.</summary>
        Task<ScholarshipProgram> DefineScholarshipProgramAsync(string nameAr, string nameEn, int discountTypeId, int? maxAwards, decimal? maxTotalAmount, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-004: nomination = a Proposed grant routed to the committee.</summary>
        Task<DiscountGrant> NominateForScholarshipAsync(int studentId, int scholarshipProgramId, decimal basisValue, string reason, int proposedByUserId, string? sponsorNote = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-DIS-005: approval applies the grant — numbered discount documents against the applicable charges (never below zero
        /// remaining), forward installments reduced per BR-INS-003 when a schedule exists. Scholarship awards check the envelope
        /// (<see cref="Common.Exceptions.ScholarshipEnvelopeExhaustedException"/> unless <paramref name="envelopeOverrideReason"/>).
        /// </summary>
        Task ApproveGrantAsync(int discountGrantId, int approvedByUserId, string? envelopeOverrideReason = null, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-002: one approval covering an enumerated batch of automatic grants.</summary>
        Task ApproveGrantsAsync(IReadOnlyList<int> discountGrantIds, int approvedByUserId, CancellationToken cancellationToken = default);

        Task RejectGrantAsync(int discountGrantId, int decidedByUserId, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// The grant's percentage-of-applicable-charges equivalent — the number
        /// BR-DIS-003 routes the approval chain on, and the only honest way to
        /// compare a fixed-amount grant against a percentage threshold. Exposed
        /// because WF-04's routing value has to be the <em>same</em> number the
        /// tier was computed from: two derivations of it would eventually disagree,
        /// and the disagreement would be about who signs.
        /// </summary>
        Task<decimal> GetGrantPercentEquivalentAsync(int discountGrantId, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-008: effective date ≥ today (<see cref="Common.Exceptions.RevocationDateInPastException"/>); reason mandatory (T1). Default forgives consumed portions; <paramref name="clawBack"/> posts a manual charge for the forward fraction.</summary>
        Task RevokeGrantAsync(int discountGrantId, DateTime effectiveDate, string reason, bool clawBack = false, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-006: amount ≤ target charge remainder (<see cref="Common.Exceptions.WaiverExceedsChargeRemainderException"/>); tier by amount.</summary>
        Task<Waiver> ProposeWaiverAsync(int chargeId, WaiverKind kind, decimal amount, string reason, int proposedByUserId, decimal principalThreshold = 500m, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-006: approval issues a real credit note against the charge.</summary>
        Task DecideWaiverAsync(int waiverId, bool approve, int decidedByUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-007: queues every Approved manual/scholarship grant of <paramref name="fromAcademicYearId"/> for review before <paramref name="toAcademicYearId"/>.</summary>
        Task<IReadOnlyList<RenewalQueueItem>> BuildRenewalQueueAsync(int fromAcademicYearId, int toAcademicYearId, CancellationToken cancellationToken = default);

        /// <summary>BR-DIS-007: Approved/Adjusted creates a new Proposed grant (Source = Renewal) in the new year; Dropped ends it.</summary>
        Task DecideRenewalAsync(int renewalQueueItemId, RenewalDecision decision, int decidedByUserId, decimal? adjustedBasisValue = null, CancellationToken cancellationToken = default);
    }
}
