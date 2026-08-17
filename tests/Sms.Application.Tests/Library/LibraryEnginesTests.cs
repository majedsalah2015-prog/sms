using System;
using Sms.Application.Library;
using Sms.Domain.Library;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Library
{
    public class LibraryEnginesTests
    {
        [Fact]
        [BusinessRule("BR-LIB-003")]
        public void Checkout_needs_an_available_copy_within_limits_and_no_flags()
        {
            Assert.True(CheckoutPolicy.Evaluate(true, activeLoans: 1, maxConcurrentLoans: 2, false, false).Allowed);
            Assert.False(CheckoutPolicy.Evaluate(true, 2, 2, false, false).Allowed);
            Assert.False(CheckoutPolicy.Evaluate(false, 0, 2, false, false).Allowed);
            Assert.True(CheckoutPolicy.Evaluate(true, 0, 2, hasUnpaidFines: true, false).HasBlockingFlags);
            Assert.True(RenewalPolicy.CanRenew(0, 1, reservedByAnother: false));
            Assert.False(RenewalPolicy.CanRenew(1, 1, false));
            Assert.False(RenewalPolicy.CanRenew(0, 1, reservedByAnother: true));
        }

        [Fact]
        [BusinessRule("BR-LIB-005")]
        public void Fines_are_per_day_with_a_cap_and_zero_when_disabled()
        {
            Assert.Equal(3, FineCalculator.OverdueDays(new DateTime(2026, 10, 1), new DateTime(2026, 10, 4)));
            Assert.Equal(0, FineCalculator.OverdueDays(new DateTime(2026, 10, 5), new DateTime(2026, 10, 4)));
            Assert.Equal(1.5m, FineCalculator.Compute(3, true, 0.5m, 10m));
            Assert.Equal(10m, FineCalculator.Compute(30, true, 0.5m, 10m));
            Assert.Equal(0m, FineCalculator.Compute(30, finesEnabled: false, 0.5m, 10m));
        }

        [Fact]
        [BusinessRule("BR-LIB-006")]
        public void Replacement_uses_copy_cost_then_policy_price()
        {
            Assert.Equal(45m, ReplacementChargePolicy.Amount(45m, 30m));
            Assert.Equal(30m, ReplacementChargePolicy.Amount(null, 30m));
            Assert.Null(ReplacementChargePolicy.Amount(null, null));
        }

        [Fact]
        [BusinessRule("BR-LIB-004")]
        public void The_reservation_queue_is_first_come_first_served()
        {
            var next = ReservationQueuePolicy.NextToOffer(new[]
            {
                new ReservationQueuePolicy.Queued(7, new DateTime(2026, 10, 2)), new ReservationQueuePolicy.Queued(3, new DateTime(2026, 10, 1)),
            });

            Assert.Equal(3, next);
            Assert.True(ReservationQueuePolicy.HoldExpired(new DateTime(2026, 10, 3), new DateTime(2026, 10, 4)));
        }

        [Fact]
        [BusinessRule("BR-LIB-008")]
        public void Stocktake_findings_flag_missing_shelf_copies_and_misplaced_loaned_ones()
        {
            Assert.Equal(StocktakeFinding.Missing, StocktakeFindingEvaluator.Evaluate(CopyStatus.Available, wasScanned: false));
            Assert.Equal(StocktakeFinding.Ok, StocktakeFindingEvaluator.Evaluate(CopyStatus.Available, true));
            Assert.Equal(StocktakeFinding.Misplaced, StocktakeFindingEvaluator.Evaluate(CopyStatus.Loaned, true));
            Assert.Equal(StocktakeFinding.Ok, StocktakeFindingEvaluator.Evaluate(CopyStatus.Loaned, false));
        }
    }
}
