namespace Sms.Domain.Payroll
{
    /// <summary>قسط السلفة — one row of the repayment schedule.</summary>
    public enum SalaryAdvanceInstallmentStatus : short
    {
        /// <summary>مجدول — waiting for the month it falls due in.</summary>
        Scheduled = 1,

        /// <summary>مستقطع — carried by a payroll run that has been paid.</summary>
        Deducted = 2,

        /// <summary>معفى — the school forgave it. Closes the row without recovering the money.</summary>
        Waived = 3,
    }
}
