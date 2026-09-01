using System;
using System.Collections.Generic;
using Sms.Domain.Installments;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Where a due item came from. The product has two answers to "when is this
    /// money due", and a collection roll that reported only one of them would be
    /// wrong for half the schools that buy it.
    /// </summary>
    public enum DueItemSource
    {
        /// <summary>A row of a plan schedule: BR-INS-007's <see cref="Installment.DueDate"/> is the due date.</summary>
        Installment = 1,

        /// <summary>
        /// A posted charge no schedule covers — the unscheduled case. Its due date
        /// is its posting date, which is the same reference
        /// <c>SnapshotRefreshService.RefreshAgedReceivablesAsync</c> already ages
        /// receivables by, so the two screens cannot disagree about a family.
        /// </summary>
        UnscheduledCharge = 2,
    }

    /// <summary>
    /// One thing a family owes, reduced to the four values a collection roll
    /// actually reasons about. Deliberately not an entity: an installment and an
    /// unscheduled charge are different rows in different tables that answer the
    /// same question, and the engine should not have to know which it is holding.
    /// </summary>
    /// <param name="Source">Which table it came from — carried for the screen's benefit, never for the arithmetic.</param>
    /// <param name="DueDate">When the money fell due.</param>
    /// <param name="Amount">What was owed.</param>
    /// <param name="Settled">What has been taken off it — allocations, and for a charge also credit notes and discounts.</param>
    /// <param name="IsPdcCovered">BR-INS-009: a post-dated cheque is already in the school's hands for this one.</param>
    /// <param name="IsCollectible">False once superseded by a reschedule or written off — the money is no longer being asked for.</param>
    public sealed record DueItem(
        DueItemSource Source, DateTime DueDate, decimal Amount, decimal Settled, bool IsPdcCovered, bool IsCollectible);

    /// <summary>
    /// Pure doc/Modules/20 §8.5 / §10 and doc/Modules/19 §10: which of a family's
    /// dues fall inside an inquiry window, what is still outstanding on them, and
    /// whether that outstanding amount may be dunned.
    /// <para>
    /// It is a static engine and not a query for the usual reason — every rule
    /// below is a sentence from the docs that a school will one day dispute, and
    /// each is cheaper to pin with a unit test than to find inside a LINQ
    /// projection. The database work of *fetching* the items belongs to
    /// <see cref="ICollectionFollowUp"/>.
    /// </para>
    /// </summary>
    public static class OutstandingWindowEvaluator
    {
        /// <summary>
        /// The default page cap for a collection roll. The roll is the whole
        /// school and a grid of two thousand children helps nobody; the filters
        /// are the instrument, and the match count above the grid says what was
        /// left off.
        /// </summary>
        public const int DefaultPageSize = 200;

        /// <summary>
        /// Is the window itself answerable? An inverted range is the one filter
        /// mistake that returns an empty screen instead of an error, and an empty
        /// arrears screen reads as "nobody owes anything" — the most expensive
        /// wrong answer this module can give. Either bound may be omitted.
        /// </summary>
        public static bool IsWindowValid(DateTime? from, DateTime? to)
            => from == null || to == null || from.Value.Date <= to.Value.Date;

        /// <summary>
        /// Inclusive on both bounds, compared by date. A window of 1–31 March
        /// includes an installment due on the 31st: a collection officer asking
        /// for March means March, and an exclusive upper bound would quietly drop
        /// the month's largest instalment, which is usually the last one.
        /// </summary>
        public static bool Includes(DateTime dueDate, DateTime? from, DateTime? to)
        {
            var day = dueDate.Date;
            if (from != null && day < from.Value.Date)
            {
                return false;
            }

            return to == null || day <= to.Value.Date;
        }

        /// <summary>
        /// What is left on one item, floored at zero.
        /// <para>
        /// The floor is not defensive tidying. BR-PAY-003 allocates a receipt
        /// oldest-first across everything a payer owes, so an overpayment lands on
        /// the last item it can reach and can settle it past its own value.
        /// Summing raw differences would let that surplus silently pay down a
        /// *different* installment inside the window, and the roll would under-state
        /// what the family owes — the direction of error a school discovers only
        /// when the money never arrives.
        /// </para>
        /// </summary>
        public static decimal Outstanding(DueItem item)
        {
            if (item == null || !item.IsCollectible)
            {
                return 0m;
            }

            var left = item.Amount - item.Settled;
            return left > 0m ? left : 0m;
        }

        /// <summary>
        /// BR-INS-009: may this item be chased at all?
        /// <para>
        /// A post-dated cheque already covering it suppresses the notice — the
        /// school is holding the family's money and asking for it again is the
        /// complaint that ends a collection policy. Nothing with a zero balance is
        /// chased either, and neither is anything no longer collectible: a
        /// rescheduled row was replaced, a written-off one was forgiven, and
        /// dunning either is asking for money the school has already said it will
        /// not take.
        /// </para>
        /// </summary>
        public static bool IsNotifiable(DueItem item)
            => item != null && item.IsCollectible && !item.IsPdcCovered && Outstanding(item) > 0m;

        /// <summary>
        /// One student's position inside the window: what fell due, what is left,
        /// how much of that may be dunned, and the oldest unpaid due date — which
        /// is what <c>ReceivablesAgingBucketer</c> then buckets, so this screen's
        /// aging and the finance dashboard's agree by construction rather than by
        /// coincidence.
        /// </summary>
        /// <param name="items">Every due item for the student; the window filter is applied here, not by the caller.</param>
        public static WindowPosition Position(IEnumerable<DueItem> items, DateTime? from, DateTime? to)
        {
            var due = 0m;
            var outstanding = 0m;
            var notifiable = 0m;
            var count = 0;
            var pdcCovered = false;
            DateTime? oldest = null;

            foreach (var item in items ?? Array.Empty<DueItem>())
            {
                if (item == null || !item.IsCollectible || !Includes(item.DueDate, from, to))
                {
                    continue;
                }

                var left = Outstanding(item);
                if (left <= 0m)
                {
                    continue;
                }

                count++;
                due += item.Amount;
                outstanding += left;

                if (item.IsPdcCovered)
                {
                    pdcCovered = true;
                }
                else
                {
                    notifiable += left;
                }

                if (oldest == null || item.DueDate.Date < oldest.Value.Date)
                {
                    oldest = item.DueDate.Date;
                }
            }

            return new WindowPosition(count, due, outstanding, notifiable, oldest, pdcCovered);
        }
    }

    /// <summary>
    /// What <see cref="OutstandingWindowEvaluator.Position"/> concluded about one
    /// student.
    /// </summary>
    /// <param name="ItemCount">How many unpaid items fell in the window.</param>
    /// <param name="Due">Their face value.</param>
    /// <param name="Outstanding">What is still owed on them.</param>
    /// <param name="Notifiable">The part of <paramref name="Outstanding"/> BR-INS-009 permits a notice over.</param>
    /// <param name="OldestDueDate">The earliest unpaid due date in the window — the aging reference.</param>
    /// <param name="HasPdcCoveredItems">True when something in the window is held off by a post-dated cheque, so the screen can say why a balance cannot be chased.</param>
    public sealed record WindowPosition(
        int ItemCount, decimal Due, decimal Outstanding, decimal Notifiable, DateTime? OldestDueDate, bool HasPdcCoveredItems);
}
