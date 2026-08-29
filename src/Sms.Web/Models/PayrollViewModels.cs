using System;
using System.Collections.Generic;
using Sms.Application.Payroll;
using Sms.Domain.Payroll;

namespace Sms.Web.Models
{
    // مسير الرواتب والسلف — the screens' own shapes (owner request, 2026-08-28). Projections, not
    // entities: the grids need employee names the payroll tables do not carry, and a view that
    // takes an entity invites a controller to hand it one that is still tracked.

    /// <summary>One month on the payroll index.</summary>
    public sealed record PayrollRunRow(
        int Id,
        string RunNo,
        int PeriodYear,
        int PeriodMonth,
        DateTime PaymentDate,
        PayrollRunStatus Status,
        int LineCount,
        decimal TotalGross,
        decimal TotalNet);

    /// <summary>مسير الرواتب — the list of months, newest first.</summary>
    public sealed class PayrollIndexViewModel
    {
        public IReadOnlyList<PayrollRunRow> Runs { get; init; } = Array.Empty<PayrollRunRow>();

        /// <summary>The month the "open a run" form defaults to — the first one with no live run.</summary>
        public int DefaultYear { get; init; }

        public int DefaultMonth { get; init; }

        /// <summary>How many employees currently hold a contract, so the empty state can say what a run would produce.</summary>
        public int PayableEmployeeCount { get; init; }
    }

    /// <summary>
    /// A hand-entered adjustment as the <b>editing</b> screen needs it — carrying its id, so the
    /// row can offer a way to take it off again.
    /// <para>
    /// Distinct from <c>PayslipAdjustment</c>, which deliberately has no id: a payslip is a
    /// document handed to an employee, and nothing on it is editable.
    /// </para>
    /// </summary>
    public sealed record PayrollAdjustmentRow(int Id, PayrollAdjustmentKind Kind, string Description, decimal Amount);

    /// <summary>One employee's line on the run detail screen.</summary>
    public sealed record PayrollLineRow(
        int LineId,
        int EmployeeId,
        string EmployeeNo,
        string Name,
        decimal BasicSalary,
        decimal Allowances,
        decimal AdditionsTotal,
        decimal DeductionsTotal,
        decimal AdvanceDeduction,
        decimal GrossPay,
        decimal NetPay,
        string? Notes,
        IReadOnlyList<PayrollAdjustmentRow> Adjustments)
    {
        /// <summary>Deductions have eaten the pay — the run cannot be approved until this is fixed.</summary>
        public bool IsUnpayable => NetPay < 0m;
    }

    /// <summary>An employee who could still be added to this draft by hand.</summary>
    public sealed record PayrollCandidate(int EmployeeId, string EmployeeNo, string Name, bool HasActiveContract);

    /// <summary>مسير شهر واحد — the run, its lines, and what may be done to it now.</summary>
    public sealed class PayrollRunViewModel
    {
        public int Id { get; init; }

        public string RunNo { get; init; } = string.Empty;

        public int PeriodYear { get; init; }

        public int PeriodMonth { get; init; }

        public DateTime PaymentDate { get; init; }

        public PayrollRunStatus Status { get; init; }

        public string? Notes { get; init; }

        public DateTime? ApprovedAtUtc { get; init; }

        public DateTime? PaidAtUtc { get; init; }

        public IReadOnlyList<PayrollLineRow> Lines { get; init; } = Array.Empty<PayrollLineRow>();

        public IReadOnlyList<PayrollCandidate> Candidates { get; init; } = Array.Empty<PayrollCandidate>();

        public decimal TotalGross { get; init; }

        public decimal TotalDeductions { get; init; }

        public decimal TotalAdvanceDeduction { get; init; }

        public decimal TotalNet { get; init; }

        /// <summary>Draft only — everything that writes to a line is hidden rather than refused once the month is signed off.</summary>
        public bool IsEditable => Status == PayrollRunStatus.Draft;

        /// <summary>Any line whose deductions exceed its pay. Approval refuses while this is true, so the screen says so first.</summary>
        public bool HasUnpayableLines { get; init; }
    }

    /// <summary>
    /// Opening a month.
    /// <para>
    /// No validation attributes. Every rule these boxes could carry — a real month, a positive
    /// amount, an instalment count inside the ceiling — is already the engine's, is already refused
    /// with a sentence in the reader's language through <c>UserMessage</c>, and would be a second,
    /// English-only copy of the same rule here (the framework composes those messages from
    /// hard-coded English in every culture). The inputs carry <c>min</c>/<c>max</c>/<c>required</c>
    /// so the browser still catches a typo before the round trip.
    /// </para>
    /// </summary>
    public sealed class OpenPayrollRunForm
    {
        public int PeriodYear { get; set; }

        public int PeriodMonth { get; set; }

        public DateTime PaymentDate { get; set; }

        public string? Notes { get; set; }
    }

    /// <summary>An addition or a deduction on one payslip. See <see cref="OpenPayrollRunForm"/> on why nothing here is attributed.</summary>
    public sealed class PayrollAdjustmentForm
    {
        public int LineId { get; set; }

        public PayrollAdjustmentKind Kind { get; set; }

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }

    // ---------------------------------------------------------------- advances

    /// <summary>One advance on the advances index.</summary>
    public sealed record SalaryAdvanceRow(
        int Id,
        string AdvanceNo,
        int EmployeeId,
        string EmployeeNo,
        string Name,
        DateTime RequestDate,
        decimal Amount,
        int InstallmentCount,
        SalaryAdvanceStatus Status,
        DateTime? DisbursedOn,
        decimal Outstanding);

    /// <summary>سلف الموظفين — every advance, filtered by status.</summary>
    public sealed class AdvancesIndexViewModel
    {
        public IReadOnlyList<SalaryAdvanceRow> Advances { get; init; } = Array.Empty<SalaryAdvanceRow>();

        /// <summary>Null means "all" — the filter the screen was opened with, echoed back into the tabs.</summary>
        public SalaryAdvanceStatus? StatusFilter { get; init; }

        /// <summary>Employees who may be given an advance now — anyone without one already running.</summary>
        public IReadOnlyList<PayrollCandidate> Candidates { get; init; } = Array.Empty<PayrollCandidate>();

        public decimal TotalOutstanding { get; init; }

        public int OutstandingCount { get; init; }
    }

    /// <summary>سلفة واحدة — the request, the decision, and the repayment schedule.</summary>
    public sealed class SalaryAdvanceViewModel
    {
        public int Id { get; init; }

        public string AdvanceNo { get; init; } = string.Empty;

        public PayrollStatementEmployee Employee { get; init; } = new(0, string.Empty, string.Empty, string.Empty);

        public DateTime RequestDate { get; init; }

        public decimal Amount { get; init; }

        public int InstallmentCount { get; init; }

        public int FirstDeductionYear { get; init; }

        public int FirstDeductionMonth { get; init; }

        public SalaryAdvanceStatus Status { get; init; }

        public string? Reason { get; init; }

        public string? DecisionNote { get; init; }

        public DateTime? DecisionAtUtc { get; init; }

        public DateTime? DisbursedOn { get; init; }

        public AdvanceDisbursementMethod? DisbursementMethod { get; init; }

        public string? DisbursementRefNo { get; init; }

        public IReadOnlyList<AdvanceInstallmentView> Installments { get; init; } = Array.Empty<AdvanceInstallmentView>();

        public decimal DeductedTotal { get; init; }

        public decimal WaivedTotal { get; init; }

        public decimal OutstandingBalance { get; init; }

        /// <summary>Only a request nobody has decided may still be corrected.</summary>
        public bool IsEditable => Status == SalaryAdvanceStatus.Requested;

        public bool IsDecidable => Status == SalaryAdvanceStatus.Requested;

        public bool IsDisbursable => Status == SalaryAdvanceStatus.Approved;

        public bool IsRunning => Status == SalaryAdvanceStatus.Disbursed;
    }

    /// <summary>Requesting an advance, and correcting the request afterwards. See <see cref="OpenPayrollRunForm"/> on why nothing here is attributed.</summary>
    public sealed class SalaryAdvanceForm
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public DateTime RequestDate { get; set; }

        public decimal Amount { get; set; }

        public int InstallmentCount { get; set; } = 1;

        public int FirstDeductionYear { get; set; }

        public int FirstDeductionMonth { get; set; }

        public string? Reason { get; set; }

        /// <summary>Required when correcting an existing request — the amount is T1-audited (BR-EMP-003's category).</summary>
        public string? AuditReason { get; set; }
    }

    /// <summary>Handing the money over.</summary>
    public sealed class DisburseAdvanceForm
    {
        public int Id { get; set; }

        public DateTime DisbursedOn { get; set; }

        public AdvanceDisbursementMethod Method { get; set; } = AdvanceDisbursementMethod.Cash;

        public string? ReferenceNo { get; set; }
    }

    /// <summary>Re-cutting the remaining schedule. See <see cref="OpenPayrollRunForm"/> on why nothing here is attributed.</summary>
    public sealed class RescheduleAdvanceForm
    {
        public int Id { get; set; }

        public int InstallmentCount { get; set; }

        public int FirstDeductionYear { get; set; }

        public int FirstDeductionMonth { get; set; }

        /// <summary>
        /// Mandatory: rescheduling rewrites the T1-audited instalment count. Checked in the
        /// controller with a translated refusal rather than by <c>[Required]</c>, whose message is
        /// English in every culture — and the save would refuse anyway, in Arabic, through
        /// <c>MissingAuditReasonException</c>.
        /// </summary>
        public string AuditReason { get; set; } = string.Empty;
    }

    /// <summary>كشف السلف لموظف — the per-employee advances statement, with its picker.</summary>
    public sealed class AdvanceStatementViewModel
    {
        public EmployeeAdvanceStatement? Statement { get; init; }

        public IReadOnlyList<PayrollCandidate> Employees { get; init; } = Array.Empty<PayrollCandidate>();

        public int? SelectedEmployeeId { get; init; }

        public string SchoolNameAr { get; init; } = string.Empty;

        public string SchoolNameEn { get; init; } = string.Empty;
    }

    /// <summary>Anything printed: the school's own name heads every statement it hands out.</summary>
    public sealed class PayrollPrintViewModel<T>
    {
        public T Content { get; init; } = default!;

        public string SchoolNameAr { get; init; } = string.Empty;

        public string SchoolNameEn { get; init; } = string.Empty;

        public DateTime PrintedAtUtc { get; init; }
    }
}
