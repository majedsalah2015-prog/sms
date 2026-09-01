using Sms.Domain.Payroll;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// Pure lifecycle of a payroll run (owner request, 2026-08-28). Draft is where the arithmetic
    /// is still being argued with; Approved freezes it; Paid is terminal and is what consumes the
    /// advance instalments the run carried. Cancellation is available up to — and only up to — the
    /// point the money leaves.
    /// </summary>
    public static class PayrollRunStatusTransitions
    {
        public static bool CanTransition(PayrollRunStatus from, PayrollRunStatus to)
        {
            return (from, to) switch
            {
                (PayrollRunStatus.Draft, PayrollRunStatus.Approved) => true,
                (PayrollRunStatus.Draft, PayrollRunStatus.Cancelled) => true,
                (PayrollRunStatus.Approved, PayrollRunStatus.Paid) => true,

                // An approved run may still be pulled back: approval is a signature, not a payment,
                // and the month between the two is exactly when a missing employee turns up.
                (PayrollRunStatus.Approved, PayrollRunStatus.Draft) => true,
                (PayrollRunStatus.Approved, PayrollRunStatus.Cancelled) => true,

                // Paid is terminal. Money has moved and instalments have been consumed; a correction
                // is next month's adjustment, not a rewrite of a month that has already been paid.
                _ => false,
            };
        }

        /// <summary>Lines, adjustments and the employees on a run may only be touched while it is still a draft.</summary>
        public static bool IsEditable(PayrollRunStatus status) => status == PayrollRunStatus.Draft;
    }
}
