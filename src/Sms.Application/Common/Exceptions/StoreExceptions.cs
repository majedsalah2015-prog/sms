using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-STO-001/008: no active price for the item on the sale date — the list must be published first.</summary>
    public class StorePriceMissingException : InvalidOperationException
    {
        public StorePriceMissingException(int storeItemId)
            : base($"Store item {storeItemId} has no active price-list price (BR-STO-001).")
        {
        }
    }

    /// <summary>BR-STO-006 / doc §9: stock guard.</summary>
    public class StoreStockInsufficientException : InvalidOperationException
    {
        public StoreStockInsufficientException(int storeVariantId)
            : base($"Insufficient stock for variant {storeVariantId} (BR-STO-006).")
        {
        }
    }

    /// <summary>BR-STO-003: account-charge disabled for the category, or beyond the cap without Finance override.</summary>
    public class AccountChargeNotAllowedException : InvalidOperationException
    {
        public AccountChargeNotAllowedException(string detail)
            : base($"Account charge not allowed: {detail} (BR-STO-003).")
        {
        }
    }

    /// <summary>BR-STO-003: cash/card sales need an open till session; wallet tender needs the config and a wallet with balance.</summary>
    public class StoreTenderRejectedException : InvalidOperationException
    {
        public StoreTenderRejectedException(string detail)
            : base($"Store tender rejected: {detail} (BR-STO-003).")
        {
        }
    }

    /// <summary>BR-STO-005: outside the return window or condition rules, or quantity beyond what was sold.</summary>
    public class ReturnNotAllowedException : InvalidOperationException
    {
        public ReturnNotAllowedException(int storeSaleLineId)
            : base($"Return/exchange not allowed for sale line {storeSaleLineId} (BR-STO-005).")
        {
        }
    }

    /// <summary>BR-STO-004: handout only against charged/paid status per config.</summary>
    public class HandoutBeforeChargeException : InvalidOperationException
    {
        public HandoutBeforeChargeException(int bundleAssignmentId)
            : base($"Bundle assignment {bundleAssignmentId} is not charged yet — pay-first handout gate (BR-STO-004).")
        {
        }
    }

    /// <summary>BR-STO-008: same-session voids only.</summary>
    public class StoreSaleNotVoidableException : InvalidOperationException
    {
        public StoreSaleNotVoidableException(int storeSaleId)
            : base($"Store sale {storeSaleId} cannot be voided (BR-STO-008).")
        {
        }
    }
}
