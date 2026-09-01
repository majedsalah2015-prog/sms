using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-INS-007 half one, read from the other side of
    /// <see cref="InstallmentPaymentWaterfall"/>: how much of each installment the receipts
    /// have already covered, when the schedule spans several charges.
    /// <para>
    /// Module 21 allocates a receipt to CHARGES (BR-PAY-003), never to installments — that is
    /// what keeps one source of payment truth. So a charge's allocation is consumed by the
    /// installments claiming it in sequence order, earliest first, and an installment's covered
    /// amount is the sum of what each of its lines could take. Sharing a payment out evenly
    /// across a schedule would leave every installment partially paid and none of them settled,
    /// which is not what the family was told when they paid the first one.
    /// </para>
    /// </summary>
    public static class InstallmentCoverageCalculator
    {
        /// <summary>
        /// One scheduled-allocation line (doc/Modules/20 §7): the share of
        /// <paramref name="ChargeId"/> that the installment at <paramref name="SequenceNumber"/>
        /// claims.
        /// </summary>
        public sealed record ScheduleLine(int InstallmentId, int ChargeId, decimal Amount, int SequenceNumber);

        /// <summary>
        /// Covered amount per installment id. Installments whose lines took nothing are absent
        /// rather than present at zero — callers read this through a default lookup, and a
        /// schedule nobody has paid against should cost nothing to represent.
        /// </summary>
        public static IReadOnlyDictionary<int, decimal> Cover(
            IEnumerable<ScheduleLine> lines, IReadOnlyDictionary<int, decimal> allocatedByCharge)
        {
            var covered = new Dictionary<int, decimal>();
            foreach (var charge in lines.GroupBy(l => l.ChargeId))
            {
                var pool = allocatedByCharge.TryGetValue(charge.Key, out var allocated) ? Math.Max(0m, allocated) : 0m;
                if (pool <= 0m)
                {
                    continue;
                }

                foreach (var line in charge.OrderBy(l => l.SequenceNumber))
                {
                    if (pool <= 0m)
                    {
                        break;
                    }

                    var take = Math.Min(Math.Max(0m, line.Amount), pool);
                    pool -= take;
                    covered[line.InstallmentId] = covered.TryGetValue(line.InstallmentId, out var already) ? already + take : take;
                }
            }

            return covered;
        }
    }
}
