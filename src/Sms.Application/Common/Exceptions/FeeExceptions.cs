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
}
