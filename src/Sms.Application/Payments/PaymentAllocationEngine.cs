using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Payments
{
    /// <summary>Pure BR-PAY-003: default auto-allocation, oldest-due-first. Any amount left over (all open charges covered) is the payer's new advance/credit balance, not an error.</summary>
    public static class PaymentAllocationEngine
    {
        public readonly struct AllocationTarget
        {
            public AllocationTarget(int chargeId, decimal remainingBalance, DateTime postedAtUtc)
            {
                ChargeId = chargeId;
                RemainingBalance = remainingBalance;
                PostedAtUtc = postedAtUtc;
            }

            public int ChargeId { get; }

            public decimal RemainingBalance { get; }

            public DateTime PostedAtUtc { get; }
        }

        public readonly struct AllocationResult
        {
            public AllocationResult(int chargeId, decimal amount)
            {
                ChargeId = chargeId;
                Amount = amount;
            }

            public int ChargeId { get; }

            public decimal Amount { get; }
        }

        public static (IReadOnlyList<AllocationResult> Allocations, decimal Leftover) Allocate(
            decimal paymentAmount, IEnumerable<AllocationTarget> openCharges)
        {
            var remaining = paymentAmount;
            var allocations = new List<AllocationResult>();

            foreach (var charge in openCharges.OrderBy(c => c.PostedAtUtc))
            {
                if (remaining <= 0)
                {
                    break;
                }

                var amount = Math.Min(remaining, charge.RemainingBalance);
                if (amount > 0)
                {
                    allocations.Add(new AllocationResult(charge.ChargeId, amount));
                    remaining -= amount;
                }
            }

            return (allocations, remaining);
        }
    }
}
