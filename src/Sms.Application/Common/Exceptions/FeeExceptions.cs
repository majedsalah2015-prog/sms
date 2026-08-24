using System;
using Sms.Domain.Fees;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-FEE-002: the requested fee-structure-line status pair isn't legal.</summary>
    public class InvalidFeeStructureLineStatusTransitionException : InvalidOperationException
    {
        public InvalidFeeStructureLineStatusTransitionException(FeeStructureLineStatus from, FeeStructureLineStatus to)
            : base($"Fee structure line status cannot move from '{from}' to '{to}' (BR-FEE-002).")
        {
        }
    }

    /// <summary>BR-FEE-002: only an Approved line can be charged against — thrown when no approved line covers the grade-year x category pair.</summary>
    public class FeeStructureLineNotApprovedException : InvalidOperationException
    {
        public FeeStructureLineNotApprovedException(int gradeYearProfileId, int feeCategoryId)
            : base($"No approved fee structure line for grade-year profile {gradeYearProfileId} / category {feeCategoryId} (BR-FEE-002).")
        {
        }
    }

    /// <summary>BR-GLB-062: posted financial documents are immutable.</summary>
    public class ChargeNotPostedException : InvalidOperationException
    {
        public ChargeNotPostedException(int chargeId)
            : base($"Charge {chargeId} is not in Posted status (BR-GLB-062).")
        {
        }
    }

    /// <summary>Doc §9 validation rule: a credit note cannot exceed the remaining value of the charge it corrects.</summary>
    public class CreditNoteExceedsChargeException : InvalidOperationException
    {
        public CreditNoteExceedsChargeException(int chargeId)
            : base($"Credit note amount exceeds the remaining value of charge {chargeId} (doc/Modules/19 §9).")
        {
        }
    }

    /// <summary>BR-GLB-062: a charge with payments, credit notes or discounts against it can only be corrected by credit note, never voided.</summary>
    public class ChargeHasActivityException : InvalidOperationException
    {
        public ChargeHasActivityException(int chargeId)
            : base($"Charge {chargeId} has payments, credit notes or discounts against it and cannot be voided — correct it with a credit note instead (BR-GLB-062).")
        {
        }
    }
}

namespace Sms.Application.Common.Exceptions
{
    /// <summary>E-303 screens: a fee category referenced by structure lines or charges cannot be deactivated.</summary>
    public class FeeCategoryInUseException : InvalidOperationException
    {
        public FeeCategoryInUseException(int feeCategoryId, int structureLines, int charges)
            : base($"Fee category {feeCategoryId} is referenced by {structureLines} structure line(s) and {charges} charge(s) and cannot be deactivated.")
        {
        }
    }

    /// <summary>BR-FEE-002: only a Draft structure line may be edited or deleted.</summary>
    public class FeeStructureLineNotDraftException : InvalidOperationException
    {
        public FeeStructureLineNotDraftException(int feeStructureLineId)
            : base($"Fee structure line {feeStructureLineId} is approved and immutable (BR-FEE-002).")
        {
        }
    }

    /// <summary>
    /// BR-GLB-004: an approved price that has already billed somebody is not a plan
    /// any more. Withdrawing it would leave those charges pointing at a line that is
    /// no longer in the price list, and nobody able to say what they were for.
    /// </summary>
    public class FeeStructureLineInUseException : InvalidOperationException
    {
        public FeeStructureLineInUseException(int feeStructureLineId)
            : base($"Fee structure line {feeStructureLineId} has already been charged to students; reverse those charges before withdrawing it.")
        {
        }
    }

    /// <summary>doc/Modules/19 §9: one line per grade-year profile × category.</summary>
    public class FeeStructureLineAlreadyExistsException : InvalidOperationException
    {
        public FeeStructureLineAlreadyExistsException(int gradeYearProfileId, int feeCategoryId)
            : base($"A fee structure line already exists for grade-year profile {gradeYearProfileId} / category {feeCategoryId}.")
        {
        }
    }
}
