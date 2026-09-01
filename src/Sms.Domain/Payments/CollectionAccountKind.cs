namespace Sms.Domain.Payments
{
    /// <summary>
    /// Where the school's money actually lands (doc/Modules/21 §3 BR-PAY-002,
    /// §9 "method details mandatory per method"): a receipt records not only
    /// how it was paid but into which of the school's own accounts it went.
    /// <para>
    /// Two kinds, because that is the whole of what a school collects into: a
    /// bank account — the one a parent's transfer is addressed to, and the one
    /// card settlements and cheques are banked into — and a cash box, the safe
    /// behind the counter.
    /// </para>
    /// </summary>
    public enum CollectionAccountKind : short
    {
        /// <summary>
        /// حساب بنكي. The account a parent is told to transfer to; also where
        /// card settlements, cheques and end-of-day cash deposits land.
        /// </summary>
        Bank = 1,

        /// <summary>
        /// صندوق نقدي. A safe or drawer holding notes. Distinct from a till
        /// <em>session</em>, which says who was standing at which drawer and
        /// when — this says which pot of money the notes joined.
        /// </summary>
        CashBox = 2,
    }
}
