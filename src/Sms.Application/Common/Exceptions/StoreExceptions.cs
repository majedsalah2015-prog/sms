using System;
using Sms.Domain.Store;

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

    /// <summary>Why charging the sale to the family account was refused (BR-STO-003).</summary>
    public enum AccountChargeRefusal
    {
        /// <summary>The school does not let this category go on account at all.</summary>
        CategoryDisabled = 1,

        /// <summary>The basket is over the categorys cap, and only Finance can wave it through.</summary>
        CapExceeded = 2,
    }

    /// <summary>BR-STO-003: account-charge disabled for the category, or beyond the cap without Finance override.</summary>
    public class AccountChargeNotAllowedException : InvalidOperationException
    {
        public AccountChargeNotAllowedException(AccountChargeRefusal refusal, StoreItemCategory category)
            : base($"Account charge not allowed: {(refusal == AccountChargeRefusal.CategoryDisabled ? $"category {category} disabled" : $"the {category} cap is exceeded — Finance (P2) override required")} (BR-STO-003).")
        {
            Refusal = refusal;
            Category = category;
        }

        public AccountChargeRefusal Refusal { get; }

        /// <summary>The item category the rule was configured on, as the store screens name it.</summary>
        public StoreItemCategory Category { get; }
    }

    /// <summary>Why the tender was refused at the store counter (BR-STO-003).</summary>
    public enum StoreTenderRefusal
    {
        /// <summary>Cash or card with no cashier session open (BR-PAY-001).</summary>
        TillSessionNotOpen = 1,

        /// <summary>Wallet tender is switched off, or the sale names no student to hold a wallet.</summary>
        WalletTenderUnavailable = 2,

        /// <summary>The student has no wallet.</summary>
        NoWallet = 3,

        /// <summary>The wallet does not hold the price of the basket.</summary>
        InsufficientWalletBalance = 4,

        /// <summary>A tender that becomes a charge needs a student to charge it to.</summary>
        StudentRequired = 5,
    }

    /// <summary>BR-STO-003: cash/card sales need an open till session; wallet tender needs the config and a wallet with balance.</summary>
    public class StoreTenderRejectedException : InvalidOperationException
    {
        public StoreTenderRejectedException(StoreTenderRefusal refusal)
            : base($"Store tender rejected: {Describe(refusal)} (BR-STO-003).")
        {
            Refusal = refusal;
        }

        public StoreTenderRefusal Refusal { get; }

        private static string Describe(StoreTenderRefusal refusal) => refusal switch
        {
            StoreTenderRefusal.TillSessionNotOpen => "cash/card sales need an open till session (BR-PAY-001)",
            StoreTenderRefusal.WalletTenderUnavailable => "wallet tender disabled or no student",
            StoreTenderRefusal.NoWallet => "no wallet",
            StoreTenderRefusal.InsufficientWalletBalance => "insufficient wallet balance",
            StoreTenderRefusal.StudentRequired => "charge-backed tenders need a student",
            _ => refusal.ToString(),
        };
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
