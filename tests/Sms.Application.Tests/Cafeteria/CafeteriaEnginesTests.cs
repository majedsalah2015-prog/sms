using System;
using System.Linq;
using Sms.Application.Cafeteria;
using Sms.Application.GlExport;
using Sms.Domain.Cafeteria;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Cafeteria
{
    public class CafeteriaEnginesTests
    {
        [Fact]
        [BusinessRule("BR-CAF-001")]
        public void Wallet_balance_is_the_ledger_sum_and_overdraft_is_the_only_slack()
        {
            Assert.Equal(35m, WalletBalanceCalculator.Balance(new[] { 50m, -10m, -5m }));
            Assert.True(WalletBalanceCalculator.CanAfford(10m, 0m, 10m));
            Assert.False(WalletBalanceCalculator.CanAfford(10m, 0m, 10.01m));
            Assert.True(WalletBalanceCalculator.CanAfford(10m, 5m, 15m));
        }

        [Fact]
        [BusinessRule("BR-CAF-002")]
        public void Spend_controls_evaluate_limit_blocked_categories_and_allergy_matches()
        {
            var lines = new[]
            {
                new SpendControlEvaluator.LineInput("snacks", new[] { "peanuts" }, 6m), new SpendControlEvaluator.LineInput("drinks", Array.Empty<string>(), 4m),
            };

            var verdict = SpendControlEvaluator.Evaluate(lines, spentTodayBefore: 12m, dailyLimit: 20m, new[] { "drinks" }, new[] { "Peanuts" }, allergyHardBlock: false);

            Assert.True(verdict.OverDailyLimit);
            Assert.Equal(new[] { "drinks" }, verdict.BlockedCategoriesHit);
            Assert.Equal(new[] { "peanuts" }, verdict.AllergyMatches);
            Assert.False(verdict.AllergyBlocks);
            Assert.True(SpendControlEvaluator.Evaluate(lines, 0m, null, Array.Empty<string>(), new[] { "peanuts" }, allergyHardBlock: true).AllergyBlocks);
        }

        [Fact]
        [BusinessRule("BR-CAF-008")]
        public void Banned_items_never_reach_students_but_may_be_staff_only()
        {
            Assert.False(NutritionPolicy.AllowedOnStudentMenu(NutritionClass.Banned, isStaffOnly: false));
            Assert.True(NutritionPolicy.AllowedOnStudentMenu(NutritionClass.Banned, isStaffOnly: true));
            Assert.False(NutritionPolicy.SellableToStudent(NutritionClass.Banned, true));
            Assert.True(NutritionPolicy.SellableToStudent(NutritionClass.Red, false));
        }

        [Fact]
        [BusinessRule("BR-CAF-004")]
        public void Meal_plan_redeems_once_per_day_within_window_and_cap()
        {
            var start = new DateTime(2026, 10, 1);
            var end = new DateTime(2026, 10, 31);
            Assert.True(MealPlanRedemptionPolicy.CanRedeem(new DateTime(2026, 10, 5), start, end, alreadyRedeemedToday: false, 12m, 15m));
            Assert.False(MealPlanRedemptionPolicy.CanRedeem(new DateTime(2026, 10, 5), start, end, alreadyRedeemedToday: true, 12m, 15m));
            Assert.False(MealPlanRedemptionPolicy.CanRedeem(new DateTime(2026, 11, 1), start, end, false, 12m, 15m));
            Assert.False(MealPlanRedemptionPolicy.CanRedeem(new DateTime(2026, 10, 5), start, end, false, 16m, 15m));
        }

        [Fact]
        [BusinessRule("BR-CAF-006")]
        public void Stock_level_is_the_signed_sum_and_cannot_go_negative()
        {
            Assert.Equal(7, StockLevelCalculator.Level(new[] { 10, -2, -1 }));
            Assert.True(StockLevelCalculator.CanDeduct(7, 7));
            Assert.False(StockLevelCalculator.CanDeduct(7, 8));
        }

        [Fact]
        [BusinessRule("BR-CAF-009")]
        public void Voids_are_session_bound()
        {
            Assert.True(SaleVoidPolicy.CanVoid(5, tillSessionOpen: true, SaleStatus.Posted));
            Assert.False(SaleVoidPolicy.CanVoid(5, tillSessionOpen: false, SaleStatus.Posted));
            Assert.True(SaleVoidPolicy.CanVoid(null, false, SaleStatus.Posted));
            Assert.False(SaleVoidPolicy.CanVoid(null, false, SaleStatus.Voided));
        }

        [Fact]
        [BusinessRule("BR-CAF-007")]
        public void Wallet_money_journals_as_liability_and_cafeteria_sales_as_revenue()
        {
            var journal = JournalSummaryBuilder.Build(
                Array.Empty<JournalSummaryBuilder.ChargeDoc>(), Array.Empty<JournalSummaryBuilder.CreditNoteDoc>(), Array.Empty<JournalSummaryBuilder.DiscountDoc>(),
                Array.Empty<JournalSummaryBuilder.ReceiptDoc>(), Array.Empty<JournalSummaryBuilder.RefundDoc>(),
                new[] { new JournalSummaryBuilder.WalletTopUpDoc("Cash", 100m), new JournalSummaryBuilder.WalletTopUpDoc("Cash", -20m) },
                new[] { new JournalSummaryBuilder.CafeteriaSaleDoc(true, 30m), new JournalSummaryBuilder.CafeteriaSaleDoc(false, 12m) });

            Assert.True(journal.IsBalanced);
            Assert.Equal(100m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletLiability && l.Description == "Wallet top-ups").Credit);
            Assert.Equal(20m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletLiability && l.Description == "Wallet refunds").Debit);
            Assert.Equal(30m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.WalletLiability && l.Description == "Cafeteria sales (wallet)").Debit);
            Assert.Equal(42m, journal.Lines.Single(l => l.AccountKey == GlAccountKeys.CafeteriaRevenue).Credit);
            Assert.Equal(112m, journal.Lines.Where(l => l.AccountKey == "Cash:Cash").Sum(l => l.Debit));
        }
    }
}
