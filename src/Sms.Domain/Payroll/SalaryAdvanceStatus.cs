namespace Sms.Domain.Payroll
{
    /// <summary>
    /// السلفة — from the employee's request to the last instalment recovered.
    /// <para>
    /// <see cref="Disbursed"/> rather than "Repaying" is the state a live advance sits in, because
    /// the schedule is built at disbursement: you repay money you have received, and an advance
    /// approved but not yet handed over owes nothing yet. <see cref="Settled"/> is set by the
    /// payroll run that consumes the final instalment, not by a person.
    /// </para>
    /// </summary>
    public enum SalaryAdvanceStatus : short
    {
        /// <summary>مقدمة — the employee has asked; nobody has decided.</summary>
        Requested = 1,

        /// <summary>معتمدة — decided yes, money not yet handed over.</summary>
        Approved = 2,

        /// <summary>مرفوضة — decided no. Terminal.</summary>
        Rejected = 3,

        /// <summary>مصروفة — paid to the employee, instalments scheduled, repayment running.</summary>
        Disbursed = 4,

        /// <summary>مسددة — every instalment deducted or waived. Terminal.</summary>
        Settled = 5,

        /// <summary>ملغاة — withdrawn before the money moved. Terminal.</summary>
        Cancelled = 6,
    }
}
