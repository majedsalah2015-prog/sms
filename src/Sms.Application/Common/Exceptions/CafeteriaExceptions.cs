using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-CAF-005/008: banned-class items cannot enter a student-sale menu.</summary>
    public class BannedItemOnMenuException : InvalidOperationException
    {
        public BannedItemOnMenuException(int itemId)
            : base($"Item {itemId} is nutrition-banned for student sale and cannot be on the menu (BR-CAF-008).")
        {
        }
    }

    /// <summary>BR-CAF-002/003 / doc §9: sale blocked — over wallet balance, over daily limit, blocked category, allergy hard-block, no tender, or stock.</summary>
    public class SaleBlockedException : InvalidOperationException
    {
        public SaleBlockedException(string detail)
            : base($"Sale blocked: {detail} (BR-CAF-002/003).")
        {
        }
    }

    /// <summary>BR-CAF-002: an allergy warning fired and the operator did not confirm.</summary>
    public class AllergyWarningUnconfirmedException : InvalidOperationException
    {
        public AllergyWarningUnconfirmedException(string matches)
            : base($"Allergy warning ({matches}) requires operator confirmation (BR-CAF-002).")
        {
        }
    }

    /// <summary>BR-CAF-009: voids only within the sale's still-open till session, once.</summary>
    public class SaleNotVoidableException : InvalidOperationException
    {
        public SaleNotVoidableException(int saleId)
            : base($"Sale {saleId} cannot be voided — already voided or its till session is closed (BR-CAF-009).")
        {
        }
    }

    /// <summary>BR-CAF-009: wallet adjustments need a documented reason.</summary>
    public class WalletAdjustmentReasonRequiredException : InvalidOperationException
    {
        public WalletAdjustmentReasonRequiredException(int walletId)
            : base($"Wallet {walletId} adjustment requires a documented reason (BR-CAF-009).")
        {
        }
    }

    /// <summary>BR-CAF-001: nothing to refund.</summary>
    public class WalletBalanceNotRefundableException : InvalidOperationException
    {
        public WalletBalanceNotRefundableException(int walletId)
            : base($"Wallet {walletId} has no positive balance to refund (BR-CAF-001).")
        {
        }
    }
}
