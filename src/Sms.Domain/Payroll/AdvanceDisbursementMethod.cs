namespace Sms.Domain.Payroll
{
    /// <summary>
    /// How the advance reached the employee.
    /// <para>
    /// Deliberately its own enum rather than <c>Payments.PaymentMethod</c>: that one describes
    /// money arriving from a payer at the fees counter, carries a till session with it, and gains
    /// members for reasons that have nothing to do with staff. Recording an advance handed over in
    /// cash should not be able to break when the cashier's list changes.
    /// </para>
    /// </summary>
    public enum AdvanceDisbursementMethod : short
    {
        Cash = 1,
        BankTransfer = 2,
        Cheque = 3,

        /// <summary>محفظة جوال — the wallets ppl.Employee already records for payroll.</summary>
        Wallet = 4,
    }
}
