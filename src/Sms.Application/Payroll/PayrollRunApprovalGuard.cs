using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// What has to be true before a month's payroll may be signed off (owner request, 2026-08-28).
    /// <para>
    /// One rule today, and it is the one that matters: a school cannot approve a run in which any
    /// employee's net pay is negative. That state is reachable and not rare — an advance instalment
    /// plus a fine can exceed a part-timer's month — and approving it would print a payslip
    /// claiming an employee owes the school money, which is not a thing a payroll run can decide.
    /// The fix is a person's: reduce the deduction, or waive the instalment.
    /// </para>
    /// <para>
    /// Returns the offending employees rather than throwing, so the caller can name every one of
    /// them in a single refusal instead of the school discovering them one approval at a time.
    /// </para>
    /// </summary>
    public static class PayrollRunApprovalGuard
    {
        /// <summary>The employee ids whose lines cannot be paid as they stand, in the order given.</summary>
        public static IReadOnlyList<int> FindUnpayableEmployees(
            IEnumerable<(int EmployeeId, decimal NetPay)> lines) =>
            lines.Where(line => line.NetPay < 0m).Select(line => line.EmployeeId).ToList();

        /// <summary>A run with no lines is not a run — approving one would produce an empty register and a paid month nobody was paid in.</summary>
        public static bool HasPayableContent(int lineCount) => lineCount > 0;
    }
}
