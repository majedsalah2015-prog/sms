using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-INS-002: turn a scheduled total + percentage splits + due
    /// dates into dated amounts that sum EXACTLY to the total — rounding
    /// differences are absorbed in the last installment (the doc's
    /// explicit rule). Also maps charges onto installments in order
    /// (InstallmentChargeLine amounts) so a schedule can span several
    /// charges without losing which charge each installment collects.
    /// </summary>
    public static class InstallmentScheduleBuilder
    {
        public sealed record ScheduledInstallment(int SequenceNumber, DateTime DueDate, decimal Amount);

        public sealed record ChargePortion(int ChargeId, decimal Amount);

        public sealed record ChargeLine(int InstallmentIndex, int ChargeId, decimal Amount);

        public static bool SplitsSumToHundred(IReadOnlyList<decimal> splitPercents)
            => splitPercents.Count > 0 && splitPercents.Sum() == 100m;

        public static IReadOnlyList<ScheduledInstallment> Build(decimal total, IReadOnlyList<decimal> splitPercents, IReadOnlyList<DateTime> dueDates)
        {
            if (splitPercents.Count != dueDates.Count)
            {
                throw new ArgumentException("One due date per split is required.", nameof(dueDates));
            }

            if (!SplitsSumToHundred(splitPercents))
            {
                throw new ArgumentException("Split percentages must sum to 100.", nameof(splitPercents));
            }

            var result = new List<ScheduledInstallment>(splitPercents.Count);
            var runningTotal = 0m;
            for (var i = 0; i < splitPercents.Count; i++)
            {
                var isLast = i == splitPercents.Count - 1;
                var amount = isLast
                    ? total - runningTotal
                    : Math.Round(total * splitPercents[i] / 100m, 2, MidpointRounding.AwayFromZero);
                runningTotal += amount;
                result.Add(new ScheduledInstallment(i + 1, dueDates[i].Date, amount));
            }

            return result;
        }

        /// <summary>Equal split with the remainder in the last slot — used when spreading an appended charge over the open installments (BR-INS-003).</summary>
        public static IReadOnlyList<decimal> SpreadEvenly(decimal total, int slots)
        {
            if (slots <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slots));
            }

            var each = Math.Round(total / slots, 2, MidpointRounding.AwayFromZero);
            var amounts = Enumerable.Repeat(each, slots).ToList();
            amounts[slots - 1] = total - each * (slots - 1);
            return amounts;
        }

        /// <summary>Waterfall charges (in the given order) into installments (in the given order): every installment is filled from the earliest charge with remaining amount.</summary>
        public static IReadOnlyList<ChargeLine> MapChargesToInstallments(IReadOnlyList<ChargePortion> charges, IReadOnlyList<decimal> installmentAmounts)
        {
            if (charges.Sum(c => c.Amount) != installmentAmounts.Sum())
            {
                throw new ArgumentException("Charges and installments must total the same amount.", nameof(installmentAmounts));
            }

            var lines = new List<ChargeLine>();
            var chargeIndex = 0;
            var chargeRemaining = charges.Count > 0 ? charges[0].Amount : 0m;
            for (var i = 0; i < installmentAmounts.Count; i++)
            {
                var installmentRemaining = installmentAmounts[i];
                while (installmentRemaining > 0m && chargeIndex < charges.Count)
                {
                    if (chargeRemaining <= 0m)
                    {
                        chargeIndex++;
                        if (chargeIndex >= charges.Count)
                        {
                            break;
                        }

                        chargeRemaining = charges[chargeIndex].Amount;
                        continue;
                    }

                    var take = Math.Min(installmentRemaining, chargeRemaining);
                    lines.Add(new ChargeLine(i, charges[chargeIndex].ChargeId, take));
                    installmentRemaining -= take;
                    chargeRemaining -= take;
                }
            }

            return lines;
        }
    }
}
