namespace Sms.Domain.Payroll
{
    /// <summary>
    /// Which way a manual line on the payslip moves the money — إضافة or استقطاع.
    /// <para>
    /// Two kinds rather than one signed amount: an adjustment is entered by a person under
    /// pressure at month end, and a minus sign that goes missing is a silent overpayment, while
    /// picking the wrong item from a two-item list is a visible one.
    /// </para>
    /// </summary>
    public enum PayrollAdjustmentKind : short
    {
        /// <summary>بدل / مكافأة — overtime, a bonus, a reimbursement.</summary>
        Addition = 1,

        /// <summary>استقطاع — an unpaid-leave day, a fine, a share of an insurance premium.</summary>
        Deduction = 2,
    }
}
