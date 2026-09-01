using Sms.Domain.Payroll;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// Pure lifecycle of a salary advance (owner request, 2026-08-28).
    /// <para>
    /// The one edge worth reading twice is that <see cref="SalaryAdvanceStatus.Disbursed"/> cannot
    /// go back. Once the money is in the employee's hand the school's only route out is the
    /// repayment schedule — waiving every remaining instalment settles it and leaves a record of
    /// having done so, which cancelling would erase.
    /// </para>
    /// </summary>
    public static class SalaryAdvanceStatusTransitions
    {
        public static bool CanTransition(SalaryAdvanceStatus from, SalaryAdvanceStatus to)
        {
            return (from, to) switch
            {
                (SalaryAdvanceStatus.Requested, SalaryAdvanceStatus.Approved) => true,
                (SalaryAdvanceStatus.Requested, SalaryAdvanceStatus.Rejected) => true,
                (SalaryAdvanceStatus.Requested, SalaryAdvanceStatus.Cancelled) => true,
                (SalaryAdvanceStatus.Approved, SalaryAdvanceStatus.Disbursed) => true,
                (SalaryAdvanceStatus.Approved, SalaryAdvanceStatus.Cancelled) => true,
                (SalaryAdvanceStatus.Disbursed, SalaryAdvanceStatus.Settled) => true,
                _ => false,
            };
        }

        /// <summary>An advance still owing money — the state that blocks a second request and appears on the outstanding statement.</summary>
        public static bool IsOutstanding(SalaryAdvanceStatus status) =>
            status == SalaryAdvanceStatus.Requested
            || status == SalaryAdvanceStatus.Approved
            || status == SalaryAdvanceStatus.Disbursed;

        /// <summary>Whether a payroll run should look at this advance's schedule at all.</summary>
        public static bool IsDeductible(SalaryAdvanceStatus status) => status == SalaryAdvanceStatus.Disbursed;
    }
}
