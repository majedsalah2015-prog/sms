using System;
using System.Collections.Generic;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-INS-007 half one: how much of each installment is paid.
    /// Module 21 allocates receipts to CHARGES, not installments (one
    /// source of payment truth) — so the schedule's collected total is
    /// walked through the installments in due-date order: earliest
    /// installment fills first. Superseded/written-off installments are
    /// skipped by the caller (they're not collectible).
    /// </summary>
    public static class InstallmentPaymentWaterfall
    {
        public static IReadOnlyList<decimal> Apply(IReadOnlyList<decimal> orderedAmounts, decimal totalPaid)
        {
            var paid = new decimal[orderedAmounts.Count];
            var remaining = Math.Max(0m, totalPaid);
            for (var i = 0; i < orderedAmounts.Count && remaining > 0m; i++)
            {
                var take = Math.Min(orderedAmounts[i], remaining);
                paid[i] = take;
                remaining -= take;
            }

            return paid;
        }
    }
}
