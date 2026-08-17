using System;

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
    public class CheckoutBlockedException : InvalidOperationException
    {
        public CheckoutBlockedException(string barcode, string detail)
            : base($"Checkout of '{barcode}' blocked: {detail} (BR-LIB-003).")
        {
        }
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
        }
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
