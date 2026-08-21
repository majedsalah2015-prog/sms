using System;
using System.Collections.Generic;
using Sms.Domain.Cafeteria;
using Sms.Domain.Payments;

namespace Sms.Web.Models
{
    // ---------------------------------------------------------------- Cafeteria POS (doc/Modules/27 §8.1)

    public sealed class CafeteriaPosViewModel
    {
        public sealed record ItemCard(
            int Id, string NameAr, string NameEn, string Category, decimal Price, decimal? VatRate,
            NutritionClass NutritionClass, string? AllergenTags, bool IsStaffOnly);

        public sealed record SaleRow(int Id, string Holder, SaleTender Tender, decimal Total, decimal VatAmount, SaleStatus Status, DateTime AtUtc);

        /// <summary>
        /// Everything the operator needs before ringing anything up. Assembled on the server because
        /// the pieces come from four modules — the wallet, the spend control, the day's sales, and
        /// Health's emergency banner — and a counter is no place to discover that one of them is slow.
        /// </summary>
        public sealed class HolderCard
        {
            public int Id { get; set; }

            public WalletHolderKind Kind { get; set; }

            public string Code { get; set; } = string.Empty;

            public string NameAr { get; set; } = string.Empty;

            public string NameEn { get; set; } = string.Empty;

            public decimal WalletBalance { get; set; }

            public decimal OverdraftAllowance { get; set; }

            /// <summary>Null when the parent set no limit. Zero is a limit of zero, not the absence of one.</summary>
            public decimal? DailyLimit { get; set; }

            public decimal SpentToday { get; set; }

            public string? BlockedCategories { get; set; }

            /// <summary>BR-CAF-002: the parent opted in to a hard block, so an allergy match refuses rather than warns.</summary>
            public bool AllergyHardBlock { get; set; }

            /// <summary>From Health's emergency banner — severe allergies only, which is the set that reaches a counter.</summary>
            public string? Allergies { get; set; }

            public decimal? RemainingToday => DailyLimit == null ? null : DailyLimit.Value - SpentToday;

            /// <summary>What the wallet can still cover, overdraft included.</summary>
            public decimal Spendable => WalletBalance + OverdraftAllowance;
        }

        public IReadOnlyList<ItemCard> Items { get; set; } = Array.Empty<ItemCard>();

        public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();

        /// <summary>Empty when nothing is published for today, in which case the counter sells from the whole catalogue.</summary>
        public IReadOnlyList<int> TodaysMenuItemIds { get; set; } = Array.Empty<int>();

        public WalletHolderKind HolderKind { get; set; } = WalletHolderKind.Student;

        public HolderCard? Holder { get; set; }

        /// <summary>This cashier's own open session. Cash tender needs one (BR-CAF-007); wallet and meal plan do not.</summary>
        public TillSession? OpenTill { get; set; }

        public IReadOnlyList<SaleRow> RecentSales { get; set; } = Array.Empty<SaleRow>();
    }
}
