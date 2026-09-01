using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.GlExport
{
    /// <summary>
    /// The accounts an attached ledger will accept a posting to, offered so a
    /// finance screen can let an administrator <em>choose</em> an account instead
    /// of typing its code from memory.
    /// <para>
    /// docs/Integration/00-ERP-SMS-Integration-Analysis.md names this as the
    /// single remaining gap in the GL interface: both
    /// <c>GlAccountMapping.AccountCode</c> and <c>FeeCategory.GlExportCode</c> are
    /// free text checked against no chart at all, so a transposed digit becomes a
    /// mapping that only fails at the first posting — or worse, posts to a real
    /// but wrong account. This port closes half of it for the fee-category
    /// catalogue by making the chart visible where the code is entered.
    /// </para>
    /// <para>
    /// The read-only twin of <see cref="IGlAccountProvisioner"/>: that one asks
    /// "which account plays this role, create it if you must", this one asks
    /// "what may I post to at all". Neither writes anything the ledger would not
    /// have written itself.
    /// </para>
    /// <para>
    /// Optional, exactly like <see cref="IGlPostingPort"/>. Unregistered — which
    /// is what a standalone school system without the ERP bridge looks like —
    /// every screen that consumes it falls back to the free-text code it accepted
    /// before, because a school may still be mapping by hand to an accountant's
    /// ledger this system cannot see. An empty list therefore means "no ledger
    /// attached", never "no accounts exist".
    /// </para>
    /// </summary>
    public interface IGlAccountDirectory
    {
        /// <summary>
        /// Every active, postable account in the attached ledger's chart, in code
        /// order. Non-postable groups are excluded: they are chart structure, and
        /// offering one would produce a mapping the ledger must refuse.
        /// </summary>
        Task<IReadOnlyList<GlAccountOption>> GetPostableAccountsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// One account as the ledger describes it. Carries the name because a screen
    /// showing an operator nothing but <c>410103</c> has not told them which
    /// account they picked, and carries the nature so the screen can offer the
    /// handful that suit the role ahead of the whole chart.
    /// </summary>
    public sealed record GlAccountOption(string Code, string Name, GlAccountNature Nature = GlAccountNature.Unspecified);

    /// <summary>
    /// The five fundamental classifications of an account.
    /// <para>
    /// Values mirror the attached ledger's own classification enum one for one,
    /// including <see cref="Unspecified"/> at 0 — this is a translation of an
    /// external vocabulary, not a persisted column of this system, so the
    /// SMALLINT "start at 1" convention in CLAUDE.md does not apply and matching
    /// the source exactly is what keeps the adapter a cast rather than a lookup
    /// table that can drift.
    /// </para>
    /// <para>
    /// <see cref="Unspecified"/> means the ledger did not say, never that the
    /// account has no classification.
    /// </para>
    /// </summary>
    public enum GlAccountNature
    {
        Unspecified = 0,
        Asset = 1,
        Liability = 2,
        Equity = 3,
        Revenue = 4,
        Expense = 5,
    }
}
