using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Cafeteria;

namespace Sms.Application.Cafeteria
{
    /// <summary>Pure BR-CAF-001: balance = ledger sum; a sale is affordable within balance + the configured overdraft allowance.</summary>
    public static class WalletBalanceCalculator
    {
        public static decimal Balance(IEnumerable<decimal> signedLedgerAmounts) => signedLedgerAmounts.Sum();

        public static bool CanAfford(decimal balance, decimal overdraftAllowance, decimal amount) => balance - amount >= -overdraftAllowance;
    }

    /// <summary>Pure BR-CAF-002 real-time POS controls: daily limit (including today's earlier spend), blocked categories (hard), allergy match (warn by default, hard-block on parent opt-in).</summary>
    public static class SpendControlEvaluator
    {
        public sealed record LineInput(string Category, IReadOnlyCollection<string> AllergenTags, decimal LineTotal);

        public sealed record Verdict(bool OverDailyLimit, IReadOnlyList<string> BlockedCategoriesHit, IReadOnlyList<string> AllergyMatches, bool AllergyBlocks)
        {
            public bool Blocked => OverDailyLimit || BlockedCategoriesHit.Count > 0 || AllergyBlocks;
        }

        public static Verdict Evaluate(
            IReadOnlyCollection<LineInput> lines, decimal spentTodayBefore, decimal? dailyLimit, IReadOnlyCollection<string> blockedCategories,
            IReadOnlyCollection<string> studentAllergies, bool allergyHardBlock)
        {
            var total = lines.Sum(l => l.LineTotal);
            var overLimit = dailyLimit.HasValue && spentTodayBefore + total > dailyLimit.Value;
            var blocked = lines.Select(l => l.Category).Where(c => blockedCategories.Contains(c, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var matches = lines.SelectMany(l => l.AllergenTags)
                .Where(tag => studentAllergies.Any(a => a.Contains(tag, StringComparison.OrdinalIgnoreCase) || tag.Contains(a, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return new Verdict(overLimit, blocked, matches, matches.Count > 0 && allergyHardBlock);
        }

        public static IReadOnlyList<string> SplitTags(string? csv)
            => string.IsNullOrWhiteSpace(csv) ? Array.Empty<string>() : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Pure BR-CAF-008: banned-class items never enter a student-sale menu; staff-only items are allowed on the menu but not sold to students.</summary>
    public static class NutritionPolicy
    {
        public static bool AllowedOnStudentMenu(NutritionClass nutritionClass, bool isStaffOnly) => nutritionClass != NutritionClass.Banned || isStaffOnly;

        public static bool SellableToStudent(NutritionClass nutritionClass, bool isStaffOnly) => nutritionClass != NutritionClass.Banned && !isStaffOnly;
    }

    /// <summary>Pure BR-CAF-004: plan-first tender — one redemption per subscription per day, within the subscription window, up to the daily value cap.</summary>
    public static class MealPlanRedemptionPolicy
    {
        public static bool CanRedeem(DateTime date, DateTime startDate, DateTime endDate, bool alreadyRedeemedToday, decimal saleTotal, decimal dailyValueCap)
            => date.Date >= startDate.Date && date.Date <= endDate.Date && !alreadyRedeemedToday && saleTotal <= dailyValueCap;
    }

    /// <summary>Pure BR-CAF-006: stock level = signed movement sum; deducting below zero needs an override.</summary>
    public static class StockLevelCalculator
    {
        public static int Level(IEnumerable<int> signedQuantities) => signedQuantities.Sum();

        public static bool CanDeduct(int level, int quantity) => level - quantity >= 0;
    }

    /// <summary>Pure BR-CAF-009 (BR-PAY-002 pattern): a sale voids only within its own still-open till session.</summary>
    public static class SaleVoidPolicy
    {
        public static bool CanVoid(int? saleTillSessionId, bool tillSessionOpen, SaleStatus status)
            => status == SaleStatus.Posted && (saleTillSessionId == null || tillSessionOpen);
    }
}
