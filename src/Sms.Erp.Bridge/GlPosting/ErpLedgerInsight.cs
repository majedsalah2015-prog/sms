using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP2028.Modules.Accounting.Contracts.Analytics;
using ERP2028.Modules.Accounting.Contracts.ChartOfAccounts;
using Sms.Application.GlExport;

namespace Sms.Erp.Bridge.GlPosting
{
    /// <summary>
    /// Publishes the accountant's read of ERP 2028's ledger through
    /// <see cref="IGlLedgerInsight"/> — the route by which a chart of accounts,
    /// a trial balance headline and a list of journal entries reach the school's
    /// mobile app.
    /// <para>
    /// A straight translation, like <see cref="ErpLedgerSummary"/> and
    /// <see cref="ErpGlAccountDirectory"/> beside it. Accounting already
    /// publishes every one of these as a sanctioned query contract —
    /// <see cref="ILedgerAnalytics"/> and
    /// <see cref="IChartOfAccountsDirectory"/> — so the whole job here is
    /// renaming its vocabulary into the school's, and nothing of the ERP's
    /// crosses the seam.
    /// </para>
    /// <para>
    /// <see cref="ErpLedgerSummary"/> deliberately left these four methods
    /// behind the bridge, on the grounds that "a trial balance and a list of
    /// unposted journal entries are the accountant's screens, and this system
    /// has no business restating them". That was right for a statistics screen a
    /// head teacher opens. The mobile app answers to an accountant as well, so
    /// the methods are exposed here — behind a finance permission, read-only,
    /// and through a port of their own rather than by widening the one whose
    /// narrowness was the point.
    /// </para>
    /// </summary>
    public sealed class ErpLedgerInsight : IGlLedgerInsight
    {
        private readonly ILedgerAnalytics _analytics;
        private readonly IChartOfAccountsDirectory _chart;

        public ErpLedgerInsight(ILedgerAnalytics analytics, IChartOfAccountsDirectory chart)
        {
            _analytics = analytics;
            _chart = chart;
        }

        public async Task<IReadOnlyList<GlAccountOption>> GetChartAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await _chart.GetPostableAccountsAsync(cancellationToken);

            return accounts
                .Select(a => new GlAccountOption(a.Code, a.Name, Translate(a.Nature)))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<GlTrialBalance> GetTrialBalanceAsync(DateTime asOf, CancellationToken cancellationToken = default)
        {
            var totals = await _analytics.GetBalanceTotalsAsync(asOf, cancellationToken);
            return new GlTrialBalance(totals.Debit, totals.Credit, totals.AccountCount);
        }

        public Task<decimal> GetAccountsBalanceAsync(
            IReadOnlyCollection<string> accountCodes, DateTime asOf, CancellationToken cancellationToken = default)
        {
            // Guarded here rather than trusted to the ledger: an empty set is a
            // question with no subject, and the contract's answer to it (nothing
            // contributes) is indistinguishable from a real zero balance.
            if (accountCodes == null || accountCodes.Count == 0)
            {
                return Task.FromResult(0m);
            }

            return _analytics.GetAccountsBalanceAsync(accountCodes, asOf, cancellationToken);
        }

        public async Task<IReadOnlyList<GlEntrySummary>> GetRecentEntriesAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return Array.Empty<GlEntrySummary>();
            }

            var entries = await _analytics.GetRecentEntriesAsync(count, cancellationToken);
            return entries.Select(Translate).ToList();
        }

        public async Task<IReadOnlyList<GlEntrySummary>> GetDraftEntriesAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return Array.Empty<GlEntrySummary>();
            }

            var entries = await _analytics.GetDraftEntriesAsync(count, cancellationToken);
            return entries.Select(Translate).ToList();
        }

        private static GlEntrySummary Translate(LedgerEntrySummary entry) => new(
            entry.Number,
            entry.EntryDate,
            entry.Description,
            entry.Reference,
            entry.SourceModule,
            entry.Amount,
            Translate(entry.State),
            entry.CreatedBy);

        /// <summary>
        /// Value for value, switched rather than cast, for the same reason
        /// <see cref="ErpGlAccountDirectory"/> switches its natures: a state
        /// added on the ERP's side arrives here as
        /// <see cref="GlEntryState.Unspecified"/> — which a screen can say it
        /// does not recognise — rather than as a number this system would render
        /// as a state it does not have.
        /// </summary>
        private static GlEntryState Translate(LedgerEntryState state) => state switch
        {
            LedgerEntryState.Draft => GlEntryState.Draft,
            LedgerEntryState.Posted => GlEntryState.Posted,
            LedgerEntryState.Reversed => GlEntryState.Reversed,
            _ => GlEntryState.Unspecified,
        };

        private static GlAccountNature Translate(AccountNature nature) => nature switch
        {
            AccountNature.Asset => GlAccountNature.Asset,
            AccountNature.Liability => GlAccountNature.Liability,
            AccountNature.Equity => GlAccountNature.Equity,
            AccountNature.Revenue => GlAccountNature.Revenue,
            AccountNature.Expense => GlAccountNature.Expense,
            _ => GlAccountNature.Unspecified,
        };
    }
}
