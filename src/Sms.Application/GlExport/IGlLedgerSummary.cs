using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.GlExport
{
    /// <summary>
    /// What the attached ledger says the school earned and spent over a period —
    /// the only route by which an <em>expense</em> figure reaches a school screen
    /// (doc/Modules/31 §3, BR-DSH-002).
    /// <para>
    /// This system bills, collects and refunds; it never records what the school
    /// spends. Salaries, purchases and utilities are posted in the accounting
    /// product, so a statistics screen that wants to put "collected" next to
    /// "spent" has to ask the ledger. This port is that question, and nothing
    /// more: two totals and a monthly series, no account, no entry, no line.
    /// </para>
    /// <para>
    /// Revenue and expenses both come back as <b>positive magnitudes in their
    /// natural direction</b>, so a caller subtracts one from the other without
    /// knowing which side of the books each normally sits on. Only posted entries
    /// count — a draft is an intention, and letting one move a headline number
    /// would make this screen disagree with the trial balance.
    /// </para>
    /// <para>
    /// Optional, exactly like <see cref="IGlPostingPort"/> and
    /// <see cref="IGlAccountDirectory"/>. Unregistered — which is what a
    /// standalone school system without the ERP bridge looks like — the
    /// statistics screen shows its other four sections and says plainly that no
    /// ledger is attached. It must never show a zero: "the school spent nothing"
    /// and "nobody asked the books" are different statements, and only one of
    /// them is ever true.
    /// </para>
    /// </summary>
    public interface IGlLedgerSummary
    {
        /// <summary>
        /// Revenue and expense totals over a closed date range, both ends
        /// inclusive.
        /// </summary>
        Task<LedgerResultSummary> GetResultAsync(
            DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// One bucket per calendar month, oldest first, starting at the month
        /// containing <paramref name="firstMonth"/>. Quiet months come back as
        /// zero rather than missing, so a caller can lay the result straight onto
        /// a fixed axis without deciding what a hole means.
        /// </summary>
        Task<IReadOnlyList<LedgerMonthSummary>> GetMonthlyResultAsync(
            DateTime firstMonth, int months, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A period's result. <see cref="Net"/> is derived and never reported
    /// separately — a surplus is what is left over, not a third figure that could
    /// disagree with the two it comes from.
    /// </summary>
    public sealed record LedgerResultSummary(decimal Revenue, decimal Expenses)
    {
        public decimal Net => Revenue - Expenses;
    }

    /// <summary>One calendar month of <see cref="LedgerResultSummary"/>.</summary>
    public sealed record LedgerMonthSummary(int Year, int Month, decimal Revenue, decimal Expenses)
    {
        public decimal Net => Revenue - Expenses;
    }
}
