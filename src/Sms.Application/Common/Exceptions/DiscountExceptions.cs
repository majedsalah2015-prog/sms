using System;
using Sms.Domain.Discounts;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>The type catalog was asked to change a discount type this school does not have.</summary>
    public class DiscountTypeNotFoundException : InvalidOperationException
    {
        public DiscountTypeNotFoundException(int discountTypeId)
            : base($"Discount type {discountTypeId} does not exist in this school.")
        {
        }
    }

    /// <summary>BR-DIS-001: the proposed grant would breach the stacking policy (non-stackable type present, or combined % over the cap).</summary>
    public class DiscountStackingViolationException : InvalidOperationException
    {
        public DiscountStackingViolationException(int studentId)
            : base($"Student {studentId}'s existing grants don't allow this discount to stack (BR-DIS-001).")
        {
        }
    }

    /// <summary>BR-DIS-003 / doc §9: hardship types require restricted documentation.</summary>
    public class HardshipDocumentationRequiredException : InvalidOperationException
    {
        public HardshipDocumentationRequiredException(int discountTypeId)
            : base($"Discount type {discountTypeId} requires hardship documentation (BR-DIS-003).")
        {
        }
    }

    /// <summary>BR-DIS-004: envelope exhausted — count or amount cap — Owner override needed.</summary>
    public class ScholarshipEnvelopeExhaustedException : InvalidOperationException
    {
        public ScholarshipEnvelopeExhaustedException(int scholarshipProgramId)
            : base($"Scholarship program {scholarshipProgramId}'s envelope is exhausted (BR-DIS-004).")
        {
        }
    }

    /// <summary>WF-04: only a Proposed grant can be approved/rejected; only an Approved one revoked.</summary>
    public class InvalidDiscountGrantStateException : InvalidOperationException
    {
        public InvalidDiscountGrantStateException(int discountGrantId, DiscountGrantStatus expected)
            : base($"Discount grant {discountGrantId} is not {expected} (BR-DIS-003/008).")
        {
            Expected = expected;
        }

        /// <summary>
        /// The state the grant would have had to be in — Proposed to decide it, Approved to revoke
        /// it. Carried as the domain value, not the English word, so the Web boundary can say it in
        /// either language without matching on the sentence.
        /// </summary>
        public DiscountGrantStatus Expected { get; }
    }

    /// <summary>BR-DIS-008 / doc §9: revocation effective date ≥ today.</summary>
    public class RevocationDateInPastException : InvalidOperationException
    {
        public RevocationDateInPastException(DateTime effectiveDate)
            : base($"Revocation effective date {effectiveDate:yyyy-MM-dd} is in the past (BR-DIS-008).")
        {
            EffectiveDate = effectiveDate;
        }

        /// <summary>The date that was refused, so the message can show it back rather than describe it.</summary>
        public DateTime EffectiveDate { get; }
    }

    /// <summary>BR-DIS-006 / doc §9: waiver ≤ target charge remainder.</summary>
    public class WaiverExceedsChargeRemainderException : InvalidOperationException
    {
        public WaiverExceedsChargeRemainderException(int chargeId)
            : base($"Waiver exceeds the remaining balance of charge {chargeId} (BR-DIS-006).")
        {
        }
    }

    /// <summary>BR-DIS-006: only a Proposed waiver can be decided.</summary>
    public class WaiverNotPendingException : InvalidOperationException
    {
        public WaiverNotPendingException(int waiverId)
            : base($"Waiver {waiverId} is not pending (BR-DIS-006).")
        {
        }
    }

    /// <summary>BR-DIS-007: only a Pending renewal item can be decided.</summary>
    public class RenewalItemNotPendingException : InvalidOperationException
    {
        public RenewalItemNotPendingException(int renewalQueueItemId)
            : base($"Renewal queue item {renewalQueueItemId} is not pending (BR-DIS-007).")
        {
        }
    }
}
