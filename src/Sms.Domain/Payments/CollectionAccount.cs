using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Payments
{
    /// <summary>
    /// ppl.CollectionAccount — one of the school's own accounts that student
    /// money is collected into: a bank account a parent transfers to, or a cash
    /// box the counter takes notes into (doc/Modules/21 §3 BR-PAY-002 "method
    /// -detailed", §9 "method details mandatory per method").
    /// <para>
    /// The cashier screen had a method and a reference but no destination, so a
    /// receipt said "bank transfer, ref 88214" and nothing said <em>which</em>
    /// account it arrived in. A school with three bank accounts could not answer
    /// that from the system at all, and the cashier had no way to read the IBAN
    /// out to a parent asking where to send the money — which is the question
    /// this catalogue exists to answer.
    /// </para>
    /// <para>
    /// Distinct from <see cref="TillSession"/> on purpose: a till session says
    /// which cashier stood at which drawer between which hours (BR-PAY-001); a
    /// collection account says which pot of the school's money the payment
    /// joined. A cash receipt has both — the session that took it and the safe
    /// it went into — and they answer different questions at day close.
    /// </para>
    /// <para>
    /// Soft-active filtered: a school retires an account when it closes it at
    /// the bank, and the receipts that named it stay pointing at it forever.
    /// Read it back with <c>IgnoreQueryFilters()</c>; the picker keeps the
    /// filtered list (CLAUDE.md soft-active lookup trap).
    /// </para>
    /// <para>
    /// T2 rather than T1: it is configuration, not a money document. Nobody
    /// should have to type a reason to correct a typo in an IBAN — but every
    /// field change is kept, because an IBAN quietly edited between two
    /// transfers is exactly the change an auditor comes looking for.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T2)]
    public class CollectionAccount : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>Short operator-facing handle — "BANK-01", "SAFE-MAIN". Unique per school.</summary>
        public string Code { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public CollectionAccountKind Kind { get; set; }

        /// <summary>
        /// البنك — core.LookupValue, category "Bank", the same catalogue payroll
        /// resolves an employee's bank against. Null for a cash box, and null on
        /// a bank account whose bank was typed rather than picked (a deployment
        /// that has not filled the catalogue yet) — see <see cref="BankName"/>.
        /// </summary>
        public int? BankLookupId { get; set; }

        /// <summary>
        /// اسم البنك as free text, for a bank the "Bank" catalogue does not hold
        /// yet. The pair exists for the reason the employee record's pair does:
        /// the catalogue ships empty, and a school should not be blocked from
        /// recording its own account while it fills one in.
        /// </summary>
        public string? BankName { get; set; }

        /// <summary>رقم الحساب. Null on a cash box, which has no account number.</summary>
        public string? AccountNo { get; set; }

        /// <summary>
        /// الآيبان. Kept beside <see cref="AccountNo"/> rather than instead of
        /// it: the Gulf parent is given an IBAN, the school's own bookkeeping
        /// still quotes the account number, and a receipt may need to show
        /// either.
        /// </summary>
        public string? Iban { get; set; }

        /// <summary>
        /// README Q9 GL export interface — the ledger account this pot of money
        /// is, as free text or a code picked from an attached chart. Recorded
        /// here so the mapping is visible where the account is defined; the GL
        /// journal builder still posts by payment method (see
        /// <c>GlAccountMappingSeedContributor</c>), so this is documentation of
        /// intent until per-account posting is built.
        /// </summary>
        public string? GlExportCode { get; set; }

        /// <summary>Order in the cashier's picker — the account most money arrives in should be first.</summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Pre-selected for its kind on the cashier screen. At most one per
        /// kind per school; setting a new one clears the old.
        /// </summary>
        public bool IsDefault { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
