using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP2028.Modules.Accounting.Contracts.Analytics;
using Sms.Application.GlExport;

namespace Sms.Erp.Bridge.GlPosting
{
    /// <summary>
    /// Publishes ERP 2028's ledger result to the school's statistics screen
    /// through <see cref="IGlLedgerSummary"/> — the route by which an expense
    /// figure reaches a school page at all.
    /// <para>
    /// A straight translation, like <see cref="ErpGlAccountDirectory"/> beside it.
    /// Accounting already publishes exactly this shape as a sanctioned query
    /// contract — <see cref="ILedgerAnalytics"/>, totals only, no account, no
    /// entry, no line — so the whole job here is renaming its vocabulary into the
    /// school's. Both sides already report revenue and expense as positive
    /// magnitudes in their natural direction, so nothing is negated on the way
    /// through; the day one of them changes that, this is where it gets fixed.
    /// </para>
    /// <para>
    /// The two methods the school does not ask for —
    /// <c>GetBalanceTotalsAsync</c>, <c>GetRecentEntriesAsync</c>,
    /// <c>GetDraftEntriesAsync</c> and the per-account movement pair — stay behind
    /// the bridge. A trial balance and a list of unposted journal entries are the
    /// accountant's screens, and this system has no business restating them.
    /// </para>
    /// <para>
    /// <b>What this cannot answer.</b> The school's statistics screen would show
    /// expenses broken down by account — salaries against utilities against
    /// supplies — and <see cref="ILedgerAnalytics"/> publishes no such method:
    /// <c>GetAccountsBalanceAsync</c> nets a set of accounts into one figure
    /// rather than returning them apart. Adding one is an accounting change and
    /// belongs in the ERP repository, not here (<c>external/erp</c> is read-only
    /// in this tree). Until it lands the screen shows the totals and the monthly
    /// trend, and says so.
    /// </para>
    /// </summary>
    public sealed class ErpLedgerSummary : IGlLedgerSummary
    {
        private readonly ILedgerAnalytics _analytics;

        public ErpLedgerSummary(ILedgerAnalytics analytics)
        {
            _analytics = analytics;
        }

        public async Task<LedgerResultSummary> GetResultAsync(
            DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var result = await _analytics.GetResultAsync(fromDate, toDate, cancellationToken);
            return new LedgerResultSummary(result.Revenue, result.Expenses);
        }

        public async Task<IReadOnlyList<LedgerMonthSummary>> GetMonthlyResultAsync(
            DateTime firstMonth, int months, CancellationToken cancellationToken = default)
        {
            // Guarded here rather than trusted to the ledger: the caller derives the
            // count from an academic year's own dates, and a year saved back to front
            // would otherwise reach Accounting as a negative month count.
            if (months <= 0)
            {
                return Array.Empty<LedgerMonthSummary>();
            }

            var buckets = await _analytics.GetMonthlyResultAsync(firstMonth, months, cancellationToken);

            return buckets
                .Select(b => new LedgerMonthSummary(b.Year, b.Month, b.Revenue, b.Expenses))
                .ToList();
        }
    }
}
