using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-FEE-008 cut by BR-INS-007's dates: of the balance a student or payer carries,
    /// how much the school may actually ask for today.
    /// <para>
    /// BR-FEE-008 answers "what is owed" — posted charges less credit notes, discounts and
    /// allocations — and that figure is correct and unchanging whether or not a plan exists:
    /// scheduling a debt never reduces it. But a family given a nine-month plan does not owe
    /// the year's fee today, and a screen that prints the whole balance under "المستحق" is
    /// telling the counter something the plan contradicts. BR-INS-007 already derives the
    /// dates; this splits the one figure along them, so the plan is visible in the money and
    /// not only in the table of dates below it.
    /// </para>
    /// <para>
    /// The three terms add back to the balance exactly. That is the point: a split that did
    /// not reconcile would be a second opinion about the same debt, and the family checking
    /// their statement would find two numbers and no way to choose between them.
    /// </para>
    /// </summary>
    public static class ScheduledPositionSplitter
    {
        /// <summary>
        /// One installment as the split reads it: what it asks for, what receipts have covered
        /// (<see cref="InstallmentCoverageCalculator"/>), and the two terminal flags that take
        /// it out of collection — superseded by a reschedule, or written off under WF-06.
        /// </summary>
        public sealed record ScheduledAmount(DateTime DueDate, decimal Amount, decimal Covered, bool IsSuperseded = false, bool IsWrittenOff = false);

        /// <summary>
        /// The balance in the three parts a counter needs: what to ask for now, what the plan
        /// has moved into the future, and what has been given up on. <see cref="Unscheduled"/>
        /// is the part of <see cref="DueNow"/> that no installment claims — it is reported
        /// separately because "payable on demand" and "an installment fell due" read the same
        /// on a tile and mean different things to whoever is chasing it.
        /// </summary>
        public sealed record ScheduledPosition(decimal DueNow, decimal NotYetDue, decimal WrittenOff, decimal Unscheduled)
        {
            /// <summary>BR-FEE-008's balance, reassembled — equal to what was passed in.</summary>
            public decimal Total => DueNow + NotYetDue + WrittenOff;

            /// <summary>True when a schedule is actually holding something back from today's claim — the only case where showing the split beats showing the balance alone.</summary>
            public bool DefersAnything => NotYetDue > 0m;
        }

        /// <summary>
        /// Splits <paramref name="remaining"/> (BR-FEE-008, already net of credit notes,
        /// discounts and allocations) over <paramref name="schedule"/> as of
        /// <paramref name="today"/>.
        /// <para>
        /// The balance is the authority, not the schedule. Installments are filled from it in
        /// due-date order, so when the two have drifted apart — a credit note issued
        /// against a charge without <c>ReduceScheduleAsync</c> ever being called — the schedule
        /// gives way from its tail, which is the order BR-INS-003 reduces in anyway. Anything
        /// the schedule does not claim is on no plan at all and is payable on demand, so it
        /// joins today's figure rather than falling between the two columns.
        /// </para>
        /// <para>A payer in credit (negative balance) is reported whole as <see cref="ScheduledPosition.DueNow"/>: money already held is available now, and no installment defers it.</para>
        /// </summary>
        public static ScheduledPosition Split(decimal remaining, IReadOnlyList<ScheduledAmount> schedule, DateTime today)
        {
            if (remaining <= 0m)
            {
                return new ScheduledPosition(remaining, 0m, 0m, remaining);
            }

            var day = today.Date;
            var pot = remaining;
            var dueNow = 0m;
            var notYetDue = 0m;
            var writtenOff = 0m;

            // Superseded installments are history (BR-INS-005 keeps them readable), never a
            // claim. Earliest due date first, so a schedule that has outrun the balance gives
            // way from its tail rather than its front.
            foreach (var installment in schedule.Where(i => !i.IsSuperseded).OrderBy(i => i.DueDate))
            {
                if (pot <= 0m)
                {
                    break;
                }

                var take = Math.Min(Math.Max(0m, installment.Amount - installment.Covered), pot);
                if (take <= 0m)
                {
                    continue;
                }

                pot -= take;
                if (installment.IsWrittenOff)
                {
                    writtenOff += take;
                }
                else if (installment.DueDate.Date <= day)
                {
                    dueNow += take;
                }
                else
                {
                    notYetDue += take;
                }
            }

            return new ScheduledPosition(dueNow + pot, notYetDue, writtenOff, pot);
        }
    }
}
