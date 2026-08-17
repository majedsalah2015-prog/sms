using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-INS-003: a credit note / discount reduces the schedule —
    /// future installments first, then last-to-first (the doc's default;
    /// the alternative orderings it calls "config" aren't modeled). Paid
    /// portions never mutate: an installment can only be reduced down to
    /// what has already been collected against it.
    /// </summary>
    public static class ScheduleReductionAllocator
    {
        public sealed record OpenInstallment(int Index, DateTime DueDate, decimal Amount, decimal Paid);

        /// <summary>Returns the new amount per index for every installment that changed. Throws if the reduction exceeds the reducible remainder.</summary>
        public static IReadOnlyDictionary<int, decimal> Reduce(IReadOnlyList<OpenInstallment> installments, decimal reduction, DateTime today)
        {
            if (reduction < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(reduction));
            }

            var reducible = installments.Sum(i => i.Amount - i.Paid);
            if (reduction > reducible)
            {
                throw new InvalidOperationException($"Reduction {reduction} exceeds the unpaid remainder {reducible} (BR-INS-003).");
            }

            // Future first (due on/after today), latest due first; then the past-due ones, latest first.
            var order = installments
                .OrderBy(i => i.DueDate.Date >= today.Date ? 0 : 1)
                .ThenByDescending(i => i.DueDate)
                .ThenByDescending(i => i.Index);

            var changes = new Dictionary<int, decimal>();
            var remaining = reduction;
            foreach (var installment in order)
            {
                if (remaining <= 0m)
                {
                    break;
                }

                var room = installment.Amount - installment.Paid;
                if (room <= 0m)
                {
                    continue;
                }

                var take = Math.Min(room, remaining);
                changes[installment.Index] = installment.Amount - take;
                remaining -= take;
            }

            return changes;
        }
    }
}
