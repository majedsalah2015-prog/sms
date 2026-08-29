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

    /// <summary>Why a cafeteria sale was refused — one case per guard BR-CAF-002/003 applies.</summary>
    public enum SaleBlockReason
    {
        /// <summary>The item is not on sale to students at all.</summary>
        ItemNotSellableToStudents = 1,

        /// <summary>Todays spend for this student is already at the ceiling the parent or school set.</summary>
        DailyLimitExceeded = 2,

        /// <summary>The basket contains a category the parent or school blocked for this student.</summary>
        BlockedCategory = 3,

        /// <summary>The basket contains a declared allergen, and this one is a hard block rather than a warning.</summary>
        AllergyHardBlock = 4,

        /// <summary>Not enough of the item is left today.</summary>
        InsufficientStock = 5,

        /// <summary>The sale was tendered against a meal plan the student does not have.</summary>
        NoActiveMealPlan = 6,

        /// <summary>The meal plan is used up for today, or the basket is bigger than one days entitlement.</summary>
        MealPlanEntitlementUsed = 7,

        /// <summary>Wallet tender on a holder who has no wallet.</summary>
        NoWallet = 8,

        /// <summary>The wallet does not hold the price of the basket.</summary>
        InsufficientWalletBalance = 9,

        /// <summary>Cash was tendered with no cashier session open (BR-CAF-007).</summary>
        TillSessionNotOpen = 10,
    }

    /// <summary>
    /// BR-CAF-002/003 / doc §9: sale blocked — over wallet balance, over daily limit, blocked
    /// category, allergy hard-block, no tender, or stock.
    /// <para>
    /// The reason is a value, not a clause. This refusal is read at a till by a cashier with a
    /// queue behind them, which is the worst possible place to meet a sentence in a language you
    /// do not read; carrying the case rather than the English lets the counter screen say it in
    /// the cashiers own.
    /// </para>
    /// </summary>
    public class SaleBlockedException : InvalidOperationException
    {
        public SaleBlockedException(SaleBlockReason reason)
            : base($"Sale blocked: {Describe(reason)} (BR-CAF-002/003).")
        {
            Reason = reason;
        }

        public SaleBlockReason Reason { get; }

        private static string Describe(SaleBlockReason reason) => reason switch
        {
            SaleBlockReason.ItemNotSellableToStudents => "item is not sellable to students (BR-CAF-008)",
            SaleBlockReason.DailyLimitExceeded => "daily spend limit exceeded",
            SaleBlockReason.BlockedCategory => "the basket hits a blocked category",
            SaleBlockReason.AllergyHardBlock => "allergy hard-block",
            SaleBlockReason.InsufficientStock => "insufficient stock",
            SaleBlockReason.NoActiveMealPlan => "no active meal plan",
            SaleBlockReason.MealPlanEntitlementUsed => "meal plan already redeemed today or basket exceeds the daily entitlement",
            SaleBlockReason.NoWallet => "no wallet",
            SaleBlockReason.InsufficientWalletBalance => "insufficient wallet balance",
            SaleBlockReason.TillSessionNotOpen => "cash sales need an open till session (BR-CAF-007)",
            _ => reason.ToString(),
        };
    }

    /// <summary>BR-CAF-002: an allergy warning fired and the operator did not confirm.</summary>
    public class AllergyWarningUnconfirmedException : InvalidOperationException
    {
        public AllergyWarningUnconfirmedException(string matches)
            : base($"Allergy warning ({matches}) requires operator confirmation (BR-CAF-002).")
        {
            Matches = matches;
        }

        /// <summary>The allergens that fired, named so the cashier reads what the warning is about.</summary>
        public string Matches { get; }
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
