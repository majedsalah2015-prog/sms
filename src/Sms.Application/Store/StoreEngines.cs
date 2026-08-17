using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Store
{
    /// <summary>Pure BR-STO-003: account-charge allowed for the category and within the per-sale cap; beyond the cap needs Finance (P2).</summary>
    public static class AccountChargeEvaluator
    {
        public sealed record Verdict(bool Allowed, bool NeedsFinanceOverride);

        public static Verdict Evaluate(bool categoryAllowed, decimal? capPerSale, decimal saleTotal)
        {
            if (!categoryAllowed)
            {
                return new Verdict(false, false);
            }

            return new Verdict(true, capPerSale.HasValue && saleTotal > capPerSale.Value);
        }
    }

    /// <summary>Pure BR-STO-005: returns within the category window; sealed-only categories need a sealed item; exchanges are size swaps within the same window.</summary>
    public static class ReturnPolicyEvaluator
    {
        public static bool CanReturn(DateTime saleAtUtc, DateTime nowUtc, int windowDays, bool sealedOnly, bool isSealed)
            => nowUtc <= saleAtUtc.AddDays(windowDays) && (!sealedOnly || isSealed);
    }

    /// <summary>Pure BR-STO-006: perpetual stock; negative blocked; low-stock threshold drives the reorder report.</summary>
    public static class StoreStockPolicy
    {
        public static int Level(IEnumerable<int> signedQuantities) => signedQuantities.Sum();

        public static bool CanDeduct(int level, int quantity) => level - quantity >= 0;

        public static bool IsLow(int level, int threshold) => level <= threshold;
    }

    /// <summary>Pure BR-STO-004: a bundle is fully distributed when every line's quantity has been handed out.</summary>
    public static class HandoutCompletionEvaluator
    {
        public sealed record LineProgress(int BundleLineId, int Required, int HandedOut);

        public static bool IsComplete(IReadOnlyCollection<LineProgress> lines) => lines.All(l => l.HandedOut >= l.Required);
    }

    /// <summary>Pure BR-STO-001/008: the price is whatever the active price list says on the sale date — never an override.</summary>
    public static class PriceResolver
    {
        public sealed record ListPrice(int Version, DateTime EffectiveFrom, decimal Price);

        public static decimal? Resolve(IEnumerable<ListPrice> prices, DateTime onDate)
            => prices.Where(p => p.EffectiveFrom.Date <= onDate.Date).OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.Version).Select(p => (decimal?)p.Price).FirstOrDefault();
    }
}
