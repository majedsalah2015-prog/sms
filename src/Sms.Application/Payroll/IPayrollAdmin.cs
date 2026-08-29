using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Payroll;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// مسير الرواتب - opening a month, building its lines, adjusting them, approving and paying
    /// (owner request, 2026-08-28).
    /// <para>
    /// <b>Deviation, stated:</b> doc/Modules/12 §2 places payroll calculation out of scope
    /// (scope decision Q7) and BR-EMP-007 says the SMS never computes a net salary. This port
    /// computes one. See <c>Sms.Domain.Payroll.PayrollRun</c> for the full statement of what was
    /// asked for, what was built, and what was deliberately left out - notably that a run posts no
    /// GL journal, by the owner's choice.
    /// </para>
    /// <para>
    /// Standalone shape: every method saves itself. A payroll run is a config/admin operation with
    /// no larger transaction riding on it, which is the same call <c>SchoolAdmin</c> and
    /// <c>GradingAdmin</c> make.
    /// </para>
    /// </summary>
    public interface IPayrollAdmin
    {
        /// <summary>
        /// Opens a Draft run for one calendar month and issues its PAY number.
        /// <para>
        /// Throws <see cref="Common.Exceptions.InvalidPayrollPeriodException"/> when the period is
        /// not a real month, and <see cref="Common.Exceptions.DuplicatePayrollRunException"/> when
        /// a run that has not been cancelled already covers it - one month, one payroll.
        /// </para>
        /// </summary>
        Task<PayrollRun> OpenRunAsync(
            int periodYear, int periodMonth, DateTime paymentDate, string? notes = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Edits a draft run's intended payment date and notes. Throws
        /// <see cref="Common.Exceptions.PayrollRunNotEditableException"/> once the run has left Draft.
        /// </summary>
        Task<PayrollRun> UpdateRunAsync(
            int runId, DateTime paymentDate, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds (or rebuilds) the run's lines from every employee holding an active contract over
        /// the period, snapshotting basic and allowances and attaching the advance instalments that
        /// fall due in the month.
        /// <para>
        /// Rebuilding <b>discards hand-entered adjustments</b> along with the lines that carried
        /// them - it is a regeneration, not a merge, and pretending otherwise would leave a run
        /// half-derived from a contract that has since changed. The screen says so before it calls.
        /// </para>
        /// <para>
        /// Throws <see cref="Common.Exceptions.PayrollRunNotEditableException"/> unless the run is
        /// still a draft.
        /// </para>
        /// </summary>
        Task<PayrollRun> GenerateLinesAsync(int runId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Puts one employee on a draft run by hand - the person the contract query missed.
        /// <para>
        /// Throws <see cref="Common.Exceptions.DuplicatePayrollLineException"/> when they are
        /// already on it, <see cref="Common.Exceptions.NoActiveContractException"/> when no contract
        /// covers the period and no pay figures were supplied, and
        /// <see cref="Common.Exceptions.PayrollRunNotEditableException"/> once the run has left Draft.
        /// </para>
        /// </summary>
        Task<PayrollRunLine> AddLineAsync(
            int runId, int employeeId, decimal? basicSalary = null, decimal? allowances = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Takes an employee off a draft run, with their adjustments. A physical delete, deliberately:
        /// a draft line is working paper, not a record of anything that happened, so BR-GLB-005's
        /// no-delete rule does not reach it. Throws
        /// <see cref="Common.Exceptions.PayrollRunNotEditableException"/> once the run has left Draft.
        /// </summary>
        Task RemoveLineAsync(int lineId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds an addition or a deduction to one payslip and restates the line and run totals.
        /// <para>
        /// Throws <see cref="Common.Exceptions.NegativePayComponentException"/> for a non-positive
        /// amount - the direction is <paramref name="kind"/>'s job - and
        /// <see cref="Common.Exceptions.PayrollRunNotEditableException"/> once the run has left Draft.
        /// </para>
        /// </summary>
        Task<PayrollLineAdjustment> AddAdjustmentAsync(
            int lineId, PayrollAdjustmentKind kind, string description, decimal amount,
            CancellationToken cancellationToken = default);

        /// <summary>Removes an adjustment from a draft run and restates the totals.</summary>
        Task RemoveAdjustmentAsync(int adjustmentId, CancellationToken cancellationToken = default);

        /// <summary>Writes the note that prints on one employee's payslip.</summary>
        Task<PayrollRunLine> SetLineNotesAsync(int lineId, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs the month off, freezing the arithmetic.
        /// <para>
        /// Throws <see cref="Common.Exceptions.EmptyPayrollRunException"/> for a run with no lines,
        /// <see cref="Common.Exceptions.NegativeNetPayException"/> - naming every offending employee -
        /// when deductions exceed pay anywhere, and
        /// <see cref="Common.Exceptions.InvalidPayrollRunStatusTransitionException"/> from any state
        /// but Draft.
        /// </para>
        /// </summary>
        Task<PayrollRun> ApproveRunAsync(int runId, CancellationToken cancellationToken = default);

        /// <summary>Pulls an approved run back to Draft so it can be corrected. Refused once it is Paid.</summary>
        Task<PayrollRun> ReopenRunAsync(int runId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records that the salaries were paid. This is the step that consumes the advance
        /// instalments the run carried: each is marked Deducted against its payslip, and an advance
        /// whose last instalment has now gone is Settled.
        /// <para>
        /// Terminal. Throws
        /// <see cref="Common.Exceptions.InvalidPayrollRunStatusTransitionException"/> from any state
        /// but Approved.
        /// </para>
        /// </summary>
        Task<PayrollRun> MarkRunPaidAsync(
            int runId, DateTime paidOn, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a run that should not have been opened, freeing its month. Keeps the number and
        /// the lines - BR-GLB-005 has no Delete verb. Refused once the run is Paid.
        /// </summary>
        Task<PayrollRun> CancelRunAsync(int runId, string? reason, CancellationToken cancellationToken = default);
    }
}
