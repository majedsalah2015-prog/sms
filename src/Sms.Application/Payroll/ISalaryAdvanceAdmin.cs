using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Payroll;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// سلف الموظفين - requesting an advance, deciding it, handing the money over, and the schedule
    /// that recovers it (owner request, 2026-08-28).
    /// <para>
    /// <b>Deviation, stated:</b> nothing in doc/Modules/12 describes staff advances. The nearest
    /// the specification comes is BR-EMP-008's offboarding clearance checklist, which lists
    /// "finance advances" as something to settle on the way out and assumes they are held wherever
    /// payroll is run. See <c>Sms.Domain.Payroll.SalaryAdvance</c>.
    /// </para>
    /// <para>
    /// Repayment is by automatic payroll deduction, as the owner chose: the schedule built here is
    /// what <see cref="IPayrollAdmin.GenerateLinesAsync"/> reads, and no cash-repayment route
    /// exists. An advance the school decides to stop recovering is closed by waiving its remaining
    /// instalments, which leaves a record; there is no way to make one disappear.
    /// </para>
    /// </summary>
    public interface ISalaryAdvanceAdmin
    {
        /// <summary>
        /// Records an employee's request and issues its ADV number. Nothing is owed yet - the
        /// schedule is built at disbursement.
        /// <para>
        /// Throws <see cref="Common.Exceptions.InvalidAdvanceAmountException"/> for a non-positive
        /// amount, <see cref="Common.Exceptions.InvalidAdvanceInstallmentCountException"/> when the
        /// instalment count is outside 1..60 or would produce an instalment of nothing,
        /// <see cref="Common.Exceptions.InvalidPayrollPeriodException"/> for an unreal first
        /// deduction month, and <see cref="Common.Exceptions.OutstandingAdvanceException"/> when
        /// this employee already has one running.
        /// </para>
        /// </summary>
        Task<SalaryAdvance> RequestAsync(
            int employeeId, DateTime requestDate, decimal amount, int installmentCount,
            int firstDeductionYear, int firstDeductionMonth, string? reason = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Corrects a request that has not been decided yet. Same refusals as
        /// <see cref="RequestAsync"/>; throws
        /// <see cref="Common.Exceptions.InvalidSalaryAdvanceStatusTransitionException"/> once it has.
        /// <para>
        /// The amount and the instalment count are T1 with a required audit reason, so
        /// <c>IAuditContext.Reason</c> must be set before this is called.
        /// </para>
        /// </summary>
        Task<SalaryAdvance> UpdateRequestAsync(
            int advanceId, DateTime requestDate, decimal amount, int installmentCount,
            int firstDeductionYear, int firstDeductionMonth, string? reason,
            CancellationToken cancellationToken = default);

        /// <summary>Approves the request. The money has not moved yet - see <see cref="DisburseAsync"/>.</summary>
        Task<SalaryAdvance> ApproveAsync(int advanceId, string? note = null, CancellationToken cancellationToken = default);

        /// <summary>Refuses the request. Terminal, and the note is the only place the employee's answer is written down.</summary>
        Task<SalaryAdvance> RejectAsync(int advanceId, string? note = null, CancellationToken cancellationToken = default);

        /// <summary>Withdraws a request or an approval before any money moved. Terminal.</summary>
        Task<SalaryAdvance> CancelAsync(int advanceId, string? note = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records that the money reached the employee and <b>builds the whole repayment schedule</b>
        /// in one go, so they leave the counter knowing every instalment.
        /// <para>
        /// Throws <see cref="Common.Exceptions.InvalidSalaryAdvanceStatusTransitionException"/>
        /// unless the advance is Approved.
        /// </para>
        /// </summary>
        Task<SalaryAdvance> DisburseAsync(
            int advanceId, DateTime disbursedOn, AdvanceDisbursementMethod method,
            string? referenceNo = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forgives one scheduled instalment. Settles the advance when it was the last one open.
        /// <para>
        /// Throws <see cref="Common.Exceptions.InstallmentNotWaivableException"/> for an instalment
        /// a paid run has already recovered - that money came back, and pretending otherwise would
        /// put the advances statement out of step with the payroll register.
        /// </para>
        /// </summary>
        Task WaiveInstallmentAsync(int installmentId, string? note = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forgives everything still scheduled on an advance and settles it. The route out for a
        /// school that decides to stop recovering - it leaves the whole history in place, which is
        /// what cancelling a disbursed advance would not.
        /// </summary>
        Task<SalaryAdvance> WaiveRemainingAsync(int advanceId, string? note = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves an outstanding advance's unpaid instalments to a new schedule - the employee asks
        /// for smaller deductions, or a month is skipped.
        /// <para>
        /// Only instalments still Scheduled are replaced; anything a paid run recovered stays
        /// exactly as it was. Throws
        /// <see cref="Common.Exceptions.InvalidAdvanceInstallmentCountException"/> and
        /// <see cref="Common.Exceptions.InvalidPayrollPeriodException"/> as
        /// <see cref="RequestAsync"/> does,
        /// <see cref="Common.Exceptions.InvalidSalaryAdvanceStatusTransitionException"/> unless the
        /// advance is Disbursed, and
        /// <see cref="Common.Exceptions.InstallmentLockedByPayrollRunException"/> when one of the
        /// months being replaced already has an approved or paid payroll.
        /// </para>
        /// <para>
        /// <b>Requires <c>IAuditContext.Reason</c> to be set.</b> Rescheduling rewrites
        /// <c>InstallmentCount</c>, which is T1 with a mandatory reason — changing how much comes
        /// out of somebody's salary each month is a decision, and this is the one field that makes
        /// the system ask why. Without it the save throws <c>MissingAuditReasonException</c>.
        /// </para>
        /// </summary>
        Task<SalaryAdvance> RescheduleAsync(
            int advanceId, int installmentCount, int firstDeductionYear, int firstDeductionMonth,
            CancellationToken cancellationToken = default);
    }
}
