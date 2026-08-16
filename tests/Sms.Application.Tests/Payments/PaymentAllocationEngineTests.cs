using System;
using System.Linq;
using Sms.Application.Payments;
using Sms.TestSupport;
using Xunit;
using AllocationTarget = Sms.Application.Payments.PaymentAllocationEngine.AllocationTarget;

namespace Sms.Application.Tests.Payments
{
    public class PaymentAllocationEngineTests
    {
        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void Allocates_oldest_due_first()
        {
            var charges = new[]
            {
                new AllocationTarget(1, 500m, new DateTime(2027, 2, 1)),
                new AllocationTarget(2, 500m, new DateTime(2027, 1, 1)), // oldest
                new AllocationTarget(3, 500m, new DateTime(2027, 3, 1)),
            };

            var (allocations, leftover) = PaymentAllocationEngine.Allocate(700m, charges);

            Assert.Equal(0m, leftover);
            Assert.Equal(2, allocations.Count);
            Assert.Equal(2, allocations[0].ChargeId); // oldest charge fully paid first
            Assert.Equal(500m, allocations[0].Amount);
            Assert.Equal(1, allocations[1].ChargeId);
            Assert.Equal(200m, allocations[1].Amount);
        }

        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void Leftover_beyond_all_open_charges_becomes_the_advance_balance()
        {
            var charges = new[] { new AllocationTarget(1, 100m, new DateTime(2027, 1, 1)) };

            var (allocations, leftover) = PaymentAllocationEngine.Allocate(300m, charges);

            Assert.Single(allocations);
            Assert.Equal(100m, allocations[0].Amount);
            Assert.Equal(200m, leftover);
        }

        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void No_open_charges_leaves_the_full_amount_as_leftover()
        {
            var (allocations, leftover) = PaymentAllocationEngine.Allocate(150m, Enumerable.Empty<AllocationTarget>());

            Assert.Empty(allocations);
            Assert.Equal(150m, leftover);
        }
    }
}
