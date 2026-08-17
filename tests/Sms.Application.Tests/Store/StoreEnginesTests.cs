using System;
using Sms.Application.Store;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Store
{
    public class StoreEnginesTests
    {
        [Fact]
        [BusinessRule("BR-STO-003")]
        public void Account_charge_is_gated_by_category_and_cap()
        {
            Assert.Equal(new AccountChargeEvaluator.Verdict(false, false), AccountChargeEvaluator.Evaluate(false, 500m, 100m));
            Assert.Equal(new AccountChargeEvaluator.Verdict(true, false), AccountChargeEvaluator.Evaluate(true, 500m, 500m));
            Assert.Equal(new AccountChargeEvaluator.Verdict(true, true), AccountChargeEvaluator.Evaluate(true, 500m, 500.01m));
            Assert.Equal(new AccountChargeEvaluator.Verdict(true, false), AccountChargeEvaluator.Evaluate(true, null, 9999m));
        }

        [Fact]
        [BusinessRule("BR-STO-005")]
        public void Returns_respect_window_and_sealed_only_rules()
        {
            var sold = new DateTime(2026, 10, 1);
            Assert.True(ReturnPolicyEvaluator.CanReturn(sold, sold.AddDays(14), 14, sealedOnly: false, isSealed: false));
            Assert.False(ReturnPolicyEvaluator.CanReturn(sold, sold.AddDays(15), 14, false, false));
            Assert.False(ReturnPolicyEvaluator.CanReturn(sold, sold.AddDays(1), 14, sealedOnly: true, isSealed: false));
            Assert.True(ReturnPolicyEvaluator.CanReturn(sold, sold.AddDays(1), 14, true, isSealed: true));
        }

        [Fact]
        [BusinessRule("BR-STO-006")]
        public void Stock_is_perpetual_never_negative_and_flags_low_levels()
        {
            Assert.Equal(4, StoreStockPolicy.Level(new[] { 10, -6 }));
            Assert.False(StoreStockPolicy.CanDeduct(4, 5));
            Assert.True(StoreStockPolicy.IsLow(4, 5));
            Assert.False(StoreStockPolicy.IsLow(6, 5));
        }

        [Fact]
        [BusinessRule("BR-STO-004")]
        public void A_bundle_is_complete_only_when_every_line_is_fully_handed_out()
        {
            Assert.False(HandoutCompletionEvaluator.IsComplete(new[] { new HandoutCompletionEvaluator.LineProgress(1, 2, 1), new HandoutCompletionEvaluator.LineProgress(2, 1, 1) }));
            Assert.True(HandoutCompletionEvaluator.IsComplete(new[] { new HandoutCompletionEvaluator.LineProgress(1, 2, 2), new HandoutCompletionEvaluator.LineProgress(2, 1, 1) }));
        }

        [Fact]
        [BusinessRule("BR-STO-008")]
        public void The_price_is_the_latest_effective_list_version_never_an_override()
        {
            var prices = new[]
            {
                new PriceResolver.ListPrice(1, new DateTime(2026, 9, 1), 100m), new PriceResolver.ListPrice(2, new DateTime(2026, 10, 1), 120m), new PriceResolver.ListPrice(3, new DateTime(2027, 1, 1), 130m),
            };

            Assert.Equal(100m, PriceResolver.Resolve(prices, new DateTime(2026, 9, 15)));
            Assert.Equal(120m, PriceResolver.Resolve(prices, new DateTime(2026, 12, 31)));
            Assert.Null(PriceResolver.Resolve(prices, new DateTime(2026, 8, 1)));
        }
    }
}
