using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ERP2028.Modules.Accounting.Contracts.ChartOfAccounts;
using Sms.Application.GlExport;

namespace Sms.Erp.Bridge.GlPosting
{
    /// <summary>
    /// Publishes ERP 2028's chart of accounts to the school's finance screens
    /// through <see cref="IGlAccountDirectory"/>.
    /// <para>
    /// A straight translation and nothing more. Accounting already exposes
    /// exactly this as a sanctioned query contract —
    /// <see cref="IChartOfAccountsDirectory"/>, codes and names only, no surrogate
    /// ids, no behaviour — so the whole job here is renaming its vocabulary into
    /// the school's. That is the point of the bridge: the school names what it
    /// needs, the ERP answers, and neither compiles against the other.
    /// </para>
    /// <para>
    /// Sorted by code on this side because it is a display concern, and the
    /// contract promises no order.
    /// </para>
    /// </summary>
    public sealed class ErpGlAccountDirectory : IGlAccountDirectory
    {
        private readonly IChartOfAccountsDirectory _directory;

        public ErpGlAccountDirectory(IChartOfAccountsDirectory directory)
        {
            _directory = directory;
        }

        public async Task<IReadOnlyList<GlAccountOption>> GetPostableAccountsAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await _directory.GetPostableAccountsAsync(cancellationToken);

            return accounts
                .Select(a => new GlAccountOption(a.Code, a.Name, Translate(a.Nature)))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Value-for-value, as both enums document. Switched rather than cast so
        /// that a classification added on the ERP's side arrives here as
        /// <see cref="GlAccountNature.Unspecified"/> — the account still appears
        /// in the picker and is still postable, which is the safe failure — rather
        /// than as a number this system would render as a nature it does not have.
        /// </summary>
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
