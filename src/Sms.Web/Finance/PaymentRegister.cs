using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Web.Finance
{
    /// <summary>
    /// The two pieces of the payments register (doc/Modules/21 §10) that are wrong in ways nobody
    /// notices, kept out of the controller so a test can hold them.
    /// <para>
    /// The first is the range. A register asked for "1 to 31 March" that compares against a bare
    /// <c>DateTime</c> loses every receipt issued after midnight on the 31st — the whole last day of
    /// the month, silently, and the figure still looks like a total. <see cref="EndExclusive"/> is
    /// the half-open upper bound that makes both ends inclusive.
    /// </para>
    /// <para>
    /// The second is attribution. A receipt belongs to a payer, not a student (BR-FEE-004), and
    /// BR-PAY-003 spreads it oldest-first across everything that payer owes — so the money reaching
    /// one child is what the engine allocated to that child's invoices, and whatever is left over is
    /// the family's credit balance and belongs to no child at all. <see cref="Split"/> is that
    /// reading, and it is deliberately total-preserving: the lines always add back to the receipt,
    /// which is what lets the register's footer reconcile with the day's takings.
    /// </para>
    /// </summary>
    public static class PaymentRegister
    {
        /// <summary>
        /// Rows rendered on screen. A busy term is thousands of receipts and a grid of them helps
        /// nobody; the filters are the instrument, and the totals are computed over the whole match
        /// rather than over this page so a truncated view never reads as a complete one.
        /// </summary>
        public const int PageSize = 300;

        /// <summary>
        /// The range the screen actually reads, from what the user left in the boxes. Defaults to
        /// the month <paramref name="nowUtc"/> falls in — the period a finance office is asked
        /// about — and swaps a reversed pair rather than returning an empty register that looks
        /// like a month with no collection.
        /// </summary>
        public static (DateTime From, DateTime To) Range(DateTime? from, DateTime? to, DateTime nowUtc)
        {
            var end = (to ?? nowUtc).Date;
            var start = (from ?? new DateTime(end.Year, end.Month, 1)).Date;
            return start > end ? (end, start) : (start, end);
        }

        /// <summary>
        /// The half-open upper bound for the query: <c>IssuedAtUtc &gt;= From &amp;&amp; IssuedAtUtc &lt; EndExclusive(To)</c>.
        /// A receipt issued at any hour of <paramref name="to"/> is inside the register.
        /// </summary>
        public static DateTime EndExclusive(DateTime to) => to.Date.AddDays(1);

        /// <summary>
        /// One receipt's amount as it reached each student, plus the remainder that reached none of
        /// them. Allocations to the same student are one line; a receipt with no allocation at all
        /// is a single line with no student, because that is exactly what an advance payment is.
        /// </summary>
        /// <param name="receiptAmount">The receipt's face amount — the sum the lines must add back to.</param>
        /// <param name="allocations">This receipt's allocation lines, already resolved to the student each charge belongs to.</param>
        public static IReadOnlyList<(int? StudentId, decimal Amount)> Split(
            decimal receiptAmount,
            IEnumerable<(int StudentId, decimal Amount)> allocations)
        {
            var lines = (allocations ?? Array.Empty<(int, decimal)>())
                .GroupBy(a => a.StudentId)
                .Select(g => ((int?)g.Key, g.Sum(a => a.Amount)))
                .OrderBy(l => l.Item1)
                .ToList();

            // Only a positive remainder is a line. A receipt allocated beyond its own face value is
            // a defect in the engine, not a row for the cashier to read, and inventing a negative
            // "unallocated" line here would hide it inside a footer that still balanced.
            var remainder = receiptAmount - lines.Sum(l => l.Item2);
            if (remainder > 0m || lines.Count == 0) lines.Add((null, remainder > 0m ? remainder : 0m));
            return lines;
        }
    }
}
