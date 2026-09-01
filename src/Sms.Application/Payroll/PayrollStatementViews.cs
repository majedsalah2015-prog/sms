using System;
using System.Collections.Generic;
using Sms.Domain.Payroll;

namespace Sms.Application.Payroll
{
    // The four statements the owner asked for on 2026-08-28 - مسير الرواتب الشهري, قسيمة راتب
    // الموظف, كشف السلف, كشف التحويل البنكي - as read-only shapes the Web layer renders and the
    // exporter writes. Read models rather than entities: a statement is a question answered at a
    // moment, and nothing about it is saved.
    //
    // Names come through as an Ar/En pair rather than pre-picked, because the same statement is
    // printed in both directions and the culture that renders it is not the one that built it.

    /// <summary>Who a statement line is about, resolved once so every statement names people the same way.</summary>
    public sealed record PayrollStatementEmployee(
        int EmployeeId, string EmployeeNo, string NameAr, string NameEn);

    /// <summary>One employee's row on the monthly register.</summary>
    public sealed record PayrollRegisterLine(
        int LineId,
        PayrollStatementEmployee Employee,
        decimal BasicSalary,
        decimal Allowances,
        decimal AdditionsTotal,
        decimal DeductionsTotal,
        decimal AdvanceDeduction,
        decimal GrossPay,
        decimal NetPay);

    /// <summary>
    /// مسير الرواتب الشهري - every employee paid in one month, with the column totals a school
    /// signs at the bottom.
    /// </summary>
    public sealed record PayrollRegister(
        int RunId,
        string RunNo,
        int PeriodYear,
        int PeriodMonth,
        DateTime PaymentDate,
        PayrollRunStatus Status,
        IReadOnlyList<PayrollRegisterLine> Lines,
        decimal TotalBasic,
        decimal TotalAllowances,
        decimal TotalAdditions,
        decimal TotalDeductions,
        decimal TotalAdvanceDeduction,
        decimal TotalGross,
        decimal TotalNet);

    /// <summary>A hand-entered addition or deduction, as it prints on the payslip.</summary>
    public sealed record PayslipAdjustment(PayrollAdjustmentKind Kind, string Description, decimal Amount);

    /// <summary>The advance instalment this month recovered, named so the employee can check it against their own schedule.</summary>
    public sealed record PayslipAdvanceInstallment(
        string AdvanceNo, int SequenceNo, int InstallmentCount, decimal Amount, decimal RemainingAfterThis);

    /// <summary>
    /// قسيمة راتب الموظف - one employee, one month, with every figure broken out. The payment
    /// destination is included because the commonest question a payslip is handed back with is
    /// "which account did this go to".
    /// </summary>
    /// <param name="BankNameAr">
    /// The bank, in both languages, because the caller picks the language and this layer must not.
    /// It resolves <c>Employee.BankLookupId</c> against the "Bank" catalogue and falls back to the
    /// free-text <c>Employee.BankName</c> for a register entered before the catalogue was offered —
    /// where the one typed name is returned as both.
    /// </param>
    public sealed record Payslip(
        int LineId,
        int RunId,
        string RunNo,
        int PeriodYear,
        int PeriodMonth,
        DateTime PaymentDate,
        PayrollRunStatus RunStatus,
        PayrollStatementEmployee Employee,
        string? BankNameAr,
        string? BankNameEn,
        string? BankAccountNo,
        decimal BasicSalary,
        decimal Allowances,
        IReadOnlyList<PayslipAdjustment> Adjustments,
        IReadOnlyList<PayslipAdvanceInstallment> AdvanceInstallments,
        decimal AdditionsTotal,
        decimal DeductionsTotal,
        decimal AdvanceDeduction,
        decimal GrossPay,
        decimal NetPay,
        string? Notes);

    /// <summary>One line of the file the school hands the bank.</summary>
    /// <param name="BankNameAr">See <see cref="Payslip.BankNameAr"/> — the same pair, resolved the same way.</param>
    public sealed record BankTransferRow(
        PayrollStatementEmployee Employee,
        string? BankNameAr,
        string? BankNameEn,
        string? BankAccountNo,
        string? PalPayWalletNo,
        string? JawwalPayWalletNo,
        decimal NetPay)
    {
        /// <summary>
        /// Whether this employee has anywhere for the money to go. A row without one is not an
        /// error - some staff are paid in cash - but the bank list has to say so out loud rather
        /// than printing a blank account number and letting the bank decide.
        /// </summary>
        public bool HasDestination =>
            !string.IsNullOrWhiteSpace(BankAccountNo)
            || !string.IsNullOrWhiteSpace(PalPayWalletNo)
            || !string.IsNullOrWhiteSpace(JawwalPayWalletNo);
    }

    /// <summary>
    /// كشف التحويل البنكي - the disbursement list for one run.
    /// <para>
    /// The destinations are read live from <c>ppl.Employee</c> at the moment the list is built,
    /// not snapshotted onto the payroll line. That is right for the use this list has - it is
    /// produced to be handed to a bank now - and it means a list reprinted after somebody changed
    /// their account will show the new one. The account change is itself T1-audited with a required
    /// reason, so the two can always be reconciled.
    /// </para>
    /// </summary>
    public sealed record BankTransferList(
        int RunId,
        string RunNo,
        int PeriodYear,
        int PeriodMonth,
        DateTime PaymentDate,
        PayrollRunStatus Status,
        IReadOnlyList<BankTransferRow> Rows,
        decimal TotalNet,
        int RowsWithoutDestination);

    /// <summary>One month of one advance's schedule, with the run that consumed it named.</summary>
    public sealed record AdvanceInstallmentView(
        int InstallmentId,
        int SequenceNo,
        int DueYear,
        int DueMonth,
        decimal Amount,
        SalaryAdvanceInstallmentStatus Status,
        DateTime? DeductedAtUtc,
        string? PayrollRunNo,
        string? WaiverNote);

    /// <summary>One advance and what has become of it.</summary>
    public sealed record AdvanceView(
        int AdvanceId,
        string AdvanceNo,
        DateTime RequestDate,
        decimal Amount,
        int InstallmentCount,
        SalaryAdvanceStatus Status,
        DateTime? DisbursedOn,
        AdvanceDisbursementMethod? DisbursementMethod,
        string? Reason,
        decimal DeductedTotal,
        decimal WaivedTotal,
        decimal OutstandingBalance,
        IReadOnlyList<AdvanceInstallmentView> Installments);

    /// <summary>
    /// كشف السلف - one employee's advances, every instalment, and what is still owed.
    /// <para>
    /// An advance that was never disbursed contributes nothing to any of the totals: a request
    /// awaiting a decision is not money the employee owes.
    /// </para>
    /// </summary>
    public sealed record EmployeeAdvanceStatement(
        PayrollStatementEmployee Employee,
        IReadOnlyList<AdvanceView> Advances,
        decimal TotalAdvanced,
        decimal TotalDeducted,
        decimal TotalWaived,
        decimal TotalOutstanding);

    /// <summary>One employee's outstanding position on the school-wide advances report.</summary>
    public sealed record OutstandingAdvanceRow(
        PayrollStatementEmployee Employee,
        int AdvanceId,
        string AdvanceNo,
        DateTime? DisbursedOn,
        decimal Amount,
        decimal Deducted,
        decimal Waived,
        decimal Outstanding,
        int RemainingInstallments,
        int? NextDueYear,
        int? NextDueMonth);

    /// <summary>كشف السلف القائمة - what the school is owed by its staff, across everybody.</summary>
    public sealed record OutstandingAdvancesReport(
        IReadOnlyList<OutstandingAdvanceRow> Rows,
        decimal TotalAdvanced,
        decimal TotalDeducted,
        decimal TotalWaived,
        decimal TotalOutstanding);
}
