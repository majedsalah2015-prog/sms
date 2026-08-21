using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.GlExport
{
    /// <summary>
    /// Resolves the ledger account that plays a given role, creating a dedicated
    /// one where the role calls for it. The companion to
    /// <see cref="IGlPostingPort"/>: that one posts, this one is how the mapping
    /// table gets filled in the first place, without an administrator having to
    /// type twenty account codes correctly before the first batch can be
    /// generated.
    /// <para>
    /// The vocabulary is deliberately accounting, not ledger: this system asks
    /// for "the account student receivables belong in" and the implementation
    /// decides where that lives in its chart. Asking for a parent code here would
    /// put one chart of accounts' structure into a system that must work against
    /// any of them.
    /// </para>
    /// <para>
    /// Optional, like the posting port. Unregistered, the mapping table is filled
    /// by hand and everything downstream behaves identically.
    /// </para>
    /// </summary>
    public interface IGlAccountProvisioner
    {
        /// <summary>
        /// The account for <paramref name="role"/>, or <c>null</c> when the ledger
        /// cannot supply one.
        /// <para>
        /// <paramref name="name"/> distinguishes the instances of a repeatable
        /// role — one revenue account per fee category — and is ignored for roles
        /// that have exactly one account. Implementations must be idempotent on
        /// (role, name): seeding twice must not leave two accounts behind.
        /// </para>
        /// </summary>
        Task<GlAccountRef?> ResolveAsync(GlAccountRole role, string? name = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A ledger account as the ledger itself describes it. The name comes back
    /// with the code because the mapping table shows it to an operator, and a
    /// finance screen labelling account 5404 "BadDebtExpense" in an Arabic UI is
    /// telling them the name of an enum member, not the name of their account.
    /// </summary>
    public sealed record GlAccountRef(string Code, string Name);

    /// <summary>
    /// The accounts a school fee cycle posts to, named by what they are for
    /// rather than by where a particular chart puts them. Each maps to one
    /// <c>GlAccountKeys</c> entry.
    /// </summary>
    public enum GlAccountRole
    {
        /// <summary>What payers owe the school. A control account: the per-payer detail stays in this system.</summary>
        StudentReceivables = 1,

        /// <summary>VAT charged on fees and owed to the tax authority.</summary>
        OutputVat = 2,

        /// <summary>Discounts and scholarships granted — contra-revenue, debited in use, never netted invisibly against revenue (BR-DIS-010).</summary>
        DiscountsAllowed = 3,

        /// <summary>Money received but not yet applied to a charge. A liability until it is.</summary>
        AdvancesFromPayers = 4,

        /// <summary>Cafeteria wallet balances — payer money the school holds (BR-CAF-001/007).</summary>
        WalletLiability = 5,

        /// <summary>Fee revenue. Repeatable: one account per fee category, named by the category.</summary>
        FeeRevenue = 6,

        /// <summary>Cafeteria sales revenue.</summary>
        CafeteriaRevenue = 7,

        /// <summary>School-store sales revenue (Module 28) — uniforms, books, stationery. Its own account, not the cafeteria's: the two are separate trades and the owner reads their margins separately.</summary>
        StoreRevenue = 8,

        /// <summary>Till counting differences, over and short netted into one account (BR-PAY-001). An expense by nature; the account takes both sides.</summary>
        CashOverShort = 9,

        /// <summary>Receivables the school has given up collecting (BR-INS-010). An expense — the revenue was earned, the money is not coming.</summary>
        BadDebtExpense = 14,

        /// <summary>Hand corrections to wallet balances (BR-CAF-009). Its own account so the corrections can be reviewed as a group rather than buried in sundries.</summary>
        WalletAdjustments = 15,


        CashOnHand = 10,

        /// <summary>Card takings awaiting settlement from the acquirer — not yet in the bank.</summary>
        CardClearing = 11,

        BankAccount = 12,

        /// <summary>Cheques held, including post-dated ones, before they clear.</summary>
        ChequesReceivable = 13,
    }
}
