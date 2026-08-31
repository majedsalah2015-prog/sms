using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// The basket was approved with nothing in it. Its own refusal rather than a silent
    /// no-op: pressing "approve the student's finances" and being returned an unchanged
    /// screen reads as a failure of the system, not as a forgotten tick.
    /// </summary>
    public class EmptyFeeFileCommitException : InvalidOperationException
    {
        public EmptyFeeFileCommitException()
            : base("Nothing was selected to commit on the student's fee file.")
        {
        }
    }

    /// <summary>
    /// BR-FEE-002: the structure price is a grade-year price, so a student with no live
    /// enrollment in the working year has no price list to be billed from.
    /// </summary>
    public class StudentNotEnrolledForFeeFileException : InvalidOperationException
    {
        public StudentNotEnrolledForFeeFileException(int studentId)
            : base($"Student {studentId} has no active enrollment in the working year to price a fee file against (BR-FEE-002).")
        {
        }
    }

    /// <summary>
    /// The ticked category already carries a posted charge for this student and year — the
    /// screen was drawn before someone else billed it. Refused rather than billed twice,
    /// because a duplicate invoice is money the family is genuinely asked for.
    /// </summary>
    public class FeeItemAlreadyBilledException : InvalidOperationException
    {
        public FeeItemAlreadyBilledException(int feeCategoryId)
            : base($"Fee category {feeCategoryId} is already billed to this student for this year.")
        {
        }
    }

    /// <summary>
    /// BR-GLB-062: a posted invoice only ever moves down, by credit note. Raising an item
    /// is a new charge, and pretending otherwise would leave the increase undocumented.
    /// </summary>
    public class FeeItemAdjustmentNotLowerException : InvalidOperationException
    {
        public FeeItemAdjustmentNotLowerException(int chargeId)
            : base($"Charge {chargeId} can only be adjusted downward — raising it is a new charge (BR-GLB-062).")
        {
        }
    }

    /// <summary>Credit notes and discount documents have already relieved the whole charge; there is nothing left to remove.</summary>
    public class ChargeAlreadyFullyRelievedException : InvalidOperationException
    {
        public ChargeAlreadyFullyRelievedException(int chargeId)
            : base($"Charge {chargeId} has already been relieved in full; nothing is left to credit.")
        {
        }
    }
}
