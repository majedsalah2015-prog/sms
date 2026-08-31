using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.GlExport
{
    /// <summary>
    /// The accountant's read of the attached ledger — the chart, the trial
    /// balance's headline, the entries lately posted, and what is still sitting
    /// in draft.
    /// <para>
    /// <b>Why this is not more methods on <see cref="IGlLedgerSummary"/>.</b>
    /// That port documents itself as "two totals and a monthly series, no
    /// account, no entry, no line", and it is used by a statistics screen a
    /// head teacher opens. This one answers a different question for a different
    /// reader: it names accounts and entries, and it belongs behind a finance
    /// permission rather than a dashboard one. Widening the older port would
    /// have made every caller of it a caller of this.
    /// </para>
    /// <para>
    /// <b>Read-only, and deliberately.</b> There is no posting method here and
    /// there is not going to be one: this system bills and collects, the
    /// accounting product keeps the books, and the one write that crosses the
    /// line is <see cref="IGlPostingPort"/>'s batch — built from this system's
    /// own documents, which is a different act from an accountant writing a
    /// journal entry. A journal entry authored from the school's side belongs in
    /// the accounting product's own screens.
    /// </para>
    /// <para>
    /// Optional, exactly like every other port in this folder. Unregistered —
    /// which is what a standalone school system without the bridge looks like —
    /// the accounting endpoints answer "no ledger is attached" rather than
    /// reporting zeros. "The books are empty" and "nobody asked the books" are
    /// different statements and only one of them is ever true.
    /// </para>
    /// </summary>
    public interface IGlLedgerInsight
    {
        /// <summary>
        /// Every active, postable account. Codes and names only — no surrogate
        /// ids, because an id from another product's tables is not something
        /// this system should learn to hold.
        /// </summary>
        Task<IReadOnlyList<GlAccountOption>> GetChartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// The trial balance's two column totals as of a date, without its rows.
        /// Double entry means the two must agree; <see cref="GlTrialBalance.IsBalanced"/>
        /// is reported rather than asserted, because a reader has to be able to
        /// trust the figure without going to look for the test.
        /// </summary>
        Task<GlTrialBalance> GetTrialBalanceAsync(DateTime asOf, CancellationToken cancellationToken = default);

        /// <summary>
        /// The net balance of the named accounts as of a date: debits less
        /// credits, so an asset reads positive while it holds something. Unknown
        /// codes contribute nothing and are not an error.
        /// </summary>
        Task<decimal> GetAccountsBalanceAsync(
            IReadOnlyCollection<string> accountCodes, DateTime asOf, CancellationToken cancellationToken = default);

        /// <summary>The most recently dated <b>posted</b> entries — the "recent transactions" feed.</summary>
        Task<IReadOnlyList<GlEntrySummary>> GetRecentEntriesAsync(int count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Entries somebody started and nobody posted, oldest first — the point
        /// of the list is what has been waiting longest.
        /// </summary>
        Task<IReadOnlyList<GlEntrySummary>> GetDraftEntriesAsync(int count, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The debit and credit sides of the ledger as of a date, both positive
    /// magnitudes.
    /// </summary>
    /// <param name="Debit">Sum of the accounts that net debit.</param>
    /// <param name="Credit">Sum of the accounts that net credit.</param>
    /// <param name="AccountCount">Posting accounts carrying a non-zero balance.</param>
    public sealed record GlTrialBalance(decimal Debit, decimal Credit, int AccountCount)
    {
        /// <summary>Half a cent — ledger decimals are (18,4), so this tolerates rounding, not error.</summary>
        public const decimal Tolerance = 0.005m;

        /// <summary>Signed: positive when the debit side is the heavier one.</summary>
        public decimal Difference => Debit - Credit;

        public bool IsBalanced => Math.Abs(Difference) < Tolerance;
    }

    /// <summary>
    /// One journal entry as a list shows it. <see cref="Amount"/> is the entry's
    /// total debit — for a balanced entry that is its size, and it is the only
    /// single number that describes one.
    /// </summary>
    public sealed record GlEntrySummary(
        string? Number,
        DateTime EntryDate,
        string Description,
        string? Reference,
        string? SourceModule,
        decimal Amount,
        GlEntryState State,
        string? CreatedBy);

    /// <summary>
    /// An entry's lifecycle as this system reports it. Named here rather than
    /// borrowed, so nothing outside the bridge holds an accounting type
    /// (ErpBoundaryTests).
    /// </summary>
    public enum GlEntryState
    {
        Unspecified = 0,
        Draft = 1,
        Posted = 2,
        Reversed = 3,
    }
}
