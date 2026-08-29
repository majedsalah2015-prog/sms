using System;
using Sms.Domain.Library;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-LIB-001 / doc §9: barcode uniqueness.</summary>
    public class DuplicateBarcodeException : InvalidOperationException
    {
        public DuplicateBarcodeException(string barcode)
            : base($"Barcode '{barcode}' already exists (BR-LIB-001).")
        {
        }
    }

    /// <summary>BR-LIB-003: checkout blocked — copy unavailable, over the loan limit, or blocking flags.</summary>
    /// <summary>Why a loan was refused at the issue desk — the three guards BR-LIB-003 applies.</summary>
    public enum CheckoutBlockReason
    {
        /// <summary>The copy itself is not on the shelf to lend: already out, reserved, lost or withdrawn.</summary>
        CopyUnavailable = 1,

        /// <summary>The member is already holding as many items as their category allows.</summary>
        LoanLimitReached = 2,

        /// <summary>An unpaid fine or a clearance hold sits on the member's record.</summary>
        MemberOnHold = 3,
    }

    /// <summary>BR-LIB-003: checkout blocked — copy unavailable, over the loan limit, or blocking flags.</summary>
    public class CheckoutBlockedException : InvalidOperationException
    {
        public CheckoutBlockedException(string barcode, CheckoutBlockReason reason, CopyStatus copyStatus)
            : base($"Checkout of '{barcode}' blocked: {(reason == CheckoutBlockReason.CopyUnavailable ? $"copy is {copyStatus}" : reason == CheckoutBlockReason.LoanLimitReached ? "loan limit reached" : "unpaid fines / clearance hold")} (BR-LIB-003).")
        {
            Barcode = barcode;
            Reason = reason;
            CopyStatus = copyStatus;
        }

        /// <summary>The barcode that was scanned — the one thing on this refusal the librarian can check against the book in their hand.</summary>
        public string Barcode { get; }

        public CheckoutBlockReason Reason { get; }

        /// <summary>Where the copy actually is, when that is what blocked the loan.</summary>
        public CopyStatus CopyStatus { get; }
    }

    /// <summary>BR-LIB-003: renewals within policy unless reserved by another member.</summary>
    public class RenewalNotAllowedException : InvalidOperationException
    {
        public RenewalNotAllowedException(int loanId)
            : base($"Loan {loanId} cannot be renewed — renewal limit reached or the title is reserved (BR-LIB-003).")
        {
        }
    }

    /// <summary>BR-LIB-004: reservation limit per member policy.</summary>
    public class ReservationLimitReachedException : InvalidOperationException
    {
        public ReservationLimitReachedException(int memberId)
            : base($"Member {memberId} has reached the reservation limit (BR-LIB-004).")
        {
        }
    }

    /// <summary>BR-LIB-006 / doc §9: replacement charge requires copy cost or policy price.</summary>
    public class ReplacementPriceUnknownException : InvalidOperationException
    {
        public ReplacementPriceUnknownException(int copyId)
            : base($"Copy {copyId} has no cost and no policy price — replacement cannot be charged (BR-LIB-006).")
        {
        }
    }

    /// <summary>BR-LIB-008 / doc §9: stocktake close requires all discrepancies resolved.</summary>
    public class StocktakeUnresolvedException : InvalidOperationException
    {
        public StocktakeUnresolvedException(int sessionId, int unresolved)
            : base($"Stocktake {sessionId} has {unresolved} unresolved discrepancies (BR-LIB-008).")
        {
            Unresolved = unresolved;
        }

        /// <summary>How many copies are still unaccounted for.</summary>
        public int Unresolved { get; }
    }

    /// <summary>A loan operation on a returned/closed loan.</summary>
    public class LoanNotOpenException : InvalidOperationException
    {
        public LoanNotOpenException(int loanId)
            : base($"Loan {loanId} is not open.")
        {
        }
    }
}
