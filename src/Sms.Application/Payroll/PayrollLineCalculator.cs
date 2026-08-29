using System.Collections.Generic;
using Sms.Application.Common.Exceptions;
using Sms.Domain.Payroll;

namespace Sms.Application.Payroll
{
    /// <summary>
    /// The payslip arithmetic (owner request, 2026-08-28 — see <c>Sms.Domain.Payroll.PayrollRun</c>
    /// for the deviation from BR-EMP-007's "the SMS never computes net salary").
    /// <para>
    /// Contract-driven, as the owner chose: pay is the contract's basic plus its allowances, moved
    /// by whatever hand-entered additions and deductions the month carried, less the advance
    /// instalment falling due. There is no component catalogue and no tax engine; what a school
    /// varies month to month it enters as an adjustment.
    /// </para>
    /// </summary>
    public static class PayrollLineCalculator
    {
        /// <summary>What one payslip adds up to.</summary>
        public sealed record PayrollLineTotals(
            decimal AdditionsTotal, decimal DeductionsTotal, decimal GrossPay, decimal NetPay);

        /// <summary>
        /// Computes one line's totals.
        /// <para>
        /// The net is returned even when it comes out negative rather than throwing, because a run
        /// is generated for a whole school at once and one over-deducted employee must not stop the
        /// other two hundred lines from being written. The refusal belongs at approval, where a
        /// person can see the offending line and fix it — see
        /// <see cref="PayrollRunApprovalGuard"/>.
        /// </para>
        /// <para>
        /// Throws <see cref="NegativePayComponentException"/> when any input is negative: a
        /// negative allowance or a negative "addition" is a deduction entered in the wrong box, and
        /// silently treating it as one is how a payslip stops matching its own explanation.
        /// </para>
        /// </summary>
        public static PayrollLineTotals Calculate(
            decimal basicSalary,
            decimal allowances,
            IEnumerable<(PayrollAdjustmentKind Kind, decimal Amount)> adjustments,
            decimal advanceDeduction)
        {
            EnsureNotNegative(basicSalary, nameof(basicSalary));
            EnsureNotNegative(allowances, nameof(allowances));
            EnsureNotNegative(advanceDeduction, nameof(advanceDeduction));

            var additions = 0m;
            var deductions = 0m;

            foreach (var (kind, amount) in adjustments)
            {
                EnsureNotNegative(amount, nameof(adjustments));

                if (kind == PayrollAdjustmentKind.Addition)
                {
                    additions += amount;
                }
                else
                {
                    deductions += amount;
                }
            }

            var gross = basicSalary + allowances + additions;
            var net = gross - deductions - advanceDeduction;

            return new PayrollLineTotals(additions, deductions, gross, net);
        }

        private static void EnsureNotNegative(decimal value, string component)
        {
            if (value < 0m)
            {
                throw new NegativePayComponentException(component, value);
            }
        }
    }
}
