using System;
using System.Collections.Generic;
using Sms.Application.Common.Exceptions;
using Sms.Application.Payroll;
using Sms.Domain.Payroll;
using Xunit;

namespace Sms.Application.Tests.Payroll
{
    /// <summary>
    /// Owner request, 2026-08-28. Untagged for the reason given in
    /// <see cref="AdvanceInstallmentSchedulerTests"/>.
    /// </summary>
    public class PayrollLineCalculatorTests
    {
        private static readonly (PayrollAdjustmentKind Kind, decimal Amount)[] None =
            Array.Empty<(PayrollAdjustmentKind, decimal)>();

        [Fact]
        public void Gross_is_basic_plus_allowances_when_nothing_else_happened()
        {
            var totals = PayrollLineCalculator.Calculate(3000m, 500m, None, 0m);

            Assert.Equal(3500m, totals.GrossPay);
            Assert.Equal(3500m, totals.NetPay);
            Assert.Equal(0m, totals.AdditionsTotal);
            Assert.Equal(0m, totals.DeductionsTotal);
        }

        [Fact]
        public void Additions_raise_the_gross_and_deductions_only_the_net()
        {
            var adjustments = new[]
            {
                (PayrollAdjustmentKind.Addition, 200m),
                (PayrollAdjustmentKind.Deduction, 150m),
            };

            var totals = PayrollLineCalculator.Calculate(3000m, 500m, adjustments, 0m);

            Assert.Equal(200m, totals.AdditionsTotal);
            Assert.Equal(150m, totals.DeductionsTotal);
            Assert.Equal(3700m, totals.GrossPay);
            Assert.Equal(3550m, totals.NetPay);
        }

        [Fact]
        public void The_advance_instalment_comes_off_the_net_and_never_the_gross()
        {
            var totals = PayrollLineCalculator.Calculate(3000m, 0m, None, 250m);

            Assert.Equal(3000m, totals.GrossPay);
            Assert.Equal(2750m, totals.NetPay);

            // It is not folded into DeductionsTotal — the register reports the two separately
            // because a school asks about recovered advances on their own.
            Assert.Equal(0m, totals.DeductionsTotal);
        }

        [Fact]
        public void Several_adjustments_of_each_kind_accumulate()
        {
            var adjustments = new List<(PayrollAdjustmentKind, decimal)>
            {
                (PayrollAdjustmentKind.Addition, 100m),
                (PayrollAdjustmentKind.Addition, 50.25m),
                (PayrollAdjustmentKind.Deduction, 20m),
                (PayrollAdjustmentKind.Deduction, 5.75m),
            };

            var totals = PayrollLineCalculator.Calculate(1000m, 0m, adjustments, 0m);

            Assert.Equal(150.25m, totals.AdditionsTotal);
            Assert.Equal(25.75m, totals.DeductionsTotal);
            Assert.Equal(1150.25m, totals.GrossPay);
            Assert.Equal(1124.50m, totals.NetPay);
        }

        [Fact]
        public void A_net_that_goes_below_zero_is_returned_rather_than_thrown()
        {
            // Generation must not explode over one over-deducted employee; the refusal lives at
            // approval, where a person can see the line. See PayrollRunApprovalGuard.
            var totals = PayrollLineCalculator.Calculate(1000m, 0m, None, 1200m);

            Assert.Equal(-200m, totals.NetPay);
        }

        // --- refusals ---------------------------------------------------------

        [Fact]
        public void A_negative_basic_salary_is_refused()
        {
            Assert.Throws<NegativePayComponentException>(
                () => PayrollLineCalculator.Calculate(-1m, 0m, None, 0m));
        }

        [Fact]
        public void A_negative_allowance_is_refused()
        {
            Assert.Throws<NegativePayComponentException>(
                () => PayrollLineCalculator.Calculate(1000m, -1m, None, 0m));
        }

        [Fact]
        public void A_negative_advance_deduction_is_refused()
        {
            Assert.Throws<NegativePayComponentException>(
                () => PayrollLineCalculator.Calculate(1000m, 0m, None, -50m));
        }

        [Fact]
        public void A_negative_adjustment_is_refused_rather_than_read_as_the_other_kind()
        {
            var adjustments = new[] { (PayrollAdjustmentKind.Addition, -100m) };

            Assert.Throws<NegativePayComponentException>(
                () => PayrollLineCalculator.Calculate(1000m, 0m, adjustments, 0m));
        }
    }
}
