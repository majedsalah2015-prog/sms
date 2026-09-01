using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Application.Payroll;
using Sms.Domain.Payroll;
using Xunit;

namespace Sms.Application.Tests.Payroll
{
    /// <summary>
    /// The period arithmetic, the two status machines and the approval guard (owner request,
    /// 2026-08-28). Untagged for the reason given in <see cref="AdvanceInstallmentSchedulerTests"/>.
    /// </summary>
    public class PayrollPeriodMathTests
    {
        [Theory]
        [InlineData(2026, 1)]
        [InlineData(2026, 12)]
        [InlineData(2000, 6)]
        public void A_real_month_in_range_is_valid(int year, int month) =>
            Assert.True(PayrollPeriodMath.IsValid(year, month));

        [Theory]
        [InlineData(2026, 0)]
        [InlineData(2026, 13)]
        [InlineData(1999, 6)]
        [InlineData(20260, 6)]
        public void Anything_else_is_not(int year, int month) =>
            Assert.False(PayrollPeriodMath.IsValid(year, month));

        [Fact]
        public void EnsureValid_throws_on_a_month_that_is_not_one() =>
            Assert.Throws<InvalidPayrollPeriodException>(() => PayrollPeriodMath.EnsureValid(2026, 13));

        [Fact]
        public void Adding_months_rolls_the_year_over()
        {
            Assert.Equal((2027, 2), PayrollPeriodMath.AddMonths(2026, 11, 3));
            Assert.Equal((2026, 12), PayrollPeriodMath.AddMonths(2026, 12, 0));
            Assert.Equal((2028, 1), PayrollPeriodMath.AddMonths(2026, 1, 24));
        }

        [Fact]
        public void Adding_negative_months_walks_backwards()
        {
            Assert.Equal((2025, 12), PayrollPeriodMath.AddMonths(2026, 2, -2));
        }

        [Fact]
        public void Comparing_orders_periods_chronologically()
        {
            Assert.True(PayrollPeriodMath.Compare(2026, 1, 2026, 2) < 0);
            Assert.True(PayrollPeriodMath.Compare(2027, 1, 2026, 12) > 0);
            Assert.Equal(0, PayrollPeriodMath.Compare(2026, 6, 2026, 6));
        }
    }

    public class PayrollRunStatusTransitionsTests
    {
        [Theory]
        [InlineData(PayrollRunStatus.Draft, PayrollRunStatus.Approved)]
        [InlineData(PayrollRunStatus.Draft, PayrollRunStatus.Cancelled)]
        [InlineData(PayrollRunStatus.Approved, PayrollRunStatus.Paid)]
        [InlineData(PayrollRunStatus.Approved, PayrollRunStatus.Draft)]
        [InlineData(PayrollRunStatus.Approved, PayrollRunStatus.Cancelled)]
        public void The_legal_moves_are_allowed(PayrollRunStatus from, PayrollRunStatus to) =>
            Assert.True(PayrollRunStatusTransitions.CanTransition(from, to));

        [Theory]
        [InlineData(PayrollRunStatus.Draft, PayrollRunStatus.Paid)]
        [InlineData(PayrollRunStatus.Cancelled, PayrollRunStatus.Draft)]
        [InlineData(PayrollRunStatus.Cancelled, PayrollRunStatus.Approved)]
        public void Everything_else_is_refused(PayrollRunStatus from, PayrollRunStatus to) =>
            Assert.False(PayrollRunStatusTransitions.CanTransition(from, to));

        [Theory]
        [InlineData(PayrollRunStatus.Draft)]
        [InlineData(PayrollRunStatus.Approved)]
        [InlineData(PayrollRunStatus.Cancelled)]
        public void A_paid_run_can_never_be_left(PayrollRunStatus to) =>
            Assert.False(PayrollRunStatusTransitions.CanTransition(PayrollRunStatus.Paid, to));

        [Fact]
        public void Only_a_draft_may_be_edited()
        {
            Assert.True(PayrollRunStatusTransitions.IsEditable(PayrollRunStatus.Draft));
            Assert.False(PayrollRunStatusTransitions.IsEditable(PayrollRunStatus.Approved));
            Assert.False(PayrollRunStatusTransitions.IsEditable(PayrollRunStatus.Paid));
            Assert.False(PayrollRunStatusTransitions.IsEditable(PayrollRunStatus.Cancelled));
        }
    }

    public class SalaryAdvanceStatusTransitionsTests
    {
        [Theory]
        [InlineData(SalaryAdvanceStatus.Requested, SalaryAdvanceStatus.Approved)]
        [InlineData(SalaryAdvanceStatus.Requested, SalaryAdvanceStatus.Rejected)]
        [InlineData(SalaryAdvanceStatus.Requested, SalaryAdvanceStatus.Cancelled)]
        [InlineData(SalaryAdvanceStatus.Approved, SalaryAdvanceStatus.Disbursed)]
        [InlineData(SalaryAdvanceStatus.Approved, SalaryAdvanceStatus.Cancelled)]
        [InlineData(SalaryAdvanceStatus.Disbursed, SalaryAdvanceStatus.Settled)]
        public void The_legal_moves_are_allowed(SalaryAdvanceStatus from, SalaryAdvanceStatus to) =>
            Assert.True(SalaryAdvanceStatusTransitions.CanTransition(from, to));

        [Fact]
        public void Money_already_handed_over_cannot_be_cancelled_away()
        {
            Assert.False(SalaryAdvanceStatusTransitions.CanTransition(
                SalaryAdvanceStatus.Disbursed, SalaryAdvanceStatus.Cancelled));
        }

        [Theory]
        [InlineData(SalaryAdvanceStatus.Rejected)]
        [InlineData(SalaryAdvanceStatus.Settled)]
        [InlineData(SalaryAdvanceStatus.Cancelled)]
        public void Terminal_states_go_nowhere(SalaryAdvanceStatus from)
        {
            var everyStatus = new[]
            {
                SalaryAdvanceStatus.Requested, SalaryAdvanceStatus.Approved, SalaryAdvanceStatus.Rejected,
                SalaryAdvanceStatus.Disbursed, SalaryAdvanceStatus.Settled, SalaryAdvanceStatus.Cancelled,
            };

            Assert.All(everyStatus, to =>
                Assert.False(SalaryAdvanceStatusTransitions.CanTransition(from, to)));
        }

        [Fact]
        public void An_advance_owes_money_from_the_request_until_it_closes()
        {
            Assert.True(SalaryAdvanceStatusTransitions.IsOutstanding(SalaryAdvanceStatus.Requested));
            Assert.True(SalaryAdvanceStatusTransitions.IsOutstanding(SalaryAdvanceStatus.Approved));
            Assert.True(SalaryAdvanceStatusTransitions.IsOutstanding(SalaryAdvanceStatus.Disbursed));

            Assert.False(SalaryAdvanceStatusTransitions.IsOutstanding(SalaryAdvanceStatus.Rejected));
            Assert.False(SalaryAdvanceStatusTransitions.IsOutstanding(SalaryAdvanceStatus.Settled));
            Assert.False(SalaryAdvanceStatusTransitions.IsOutstanding(SalaryAdvanceStatus.Cancelled));
        }

        [Fact]
        public void Only_a_disbursed_advance_is_deducted_from_a_payroll_run()
        {
            Assert.True(SalaryAdvanceStatusTransitions.IsDeductible(SalaryAdvanceStatus.Disbursed));
            Assert.False(SalaryAdvanceStatusTransitions.IsDeductible(SalaryAdvanceStatus.Approved));
            Assert.False(SalaryAdvanceStatusTransitions.IsDeductible(SalaryAdvanceStatus.Settled));
        }
    }

    public class PayrollRunApprovalGuardTests
    {
        [Fact]
        public void A_run_whose_lines_all_pay_something_has_nothing_to_report()
        {
            var lines = new[] { (1, 3000m), (2, 0m), (3, 1250.50m) };

            Assert.Empty(PayrollRunApprovalGuard.FindUnpayableEmployees(lines));
        }

        [Fact]
        public void Every_over_deducted_employee_is_named_not_just_the_first()
        {
            var lines = new[] { (1, 3000m), (2, -50m), (3, 1250m), (4, -0.01m) };

            Assert.Equal(new[] { 2, 4 }, PayrollRunApprovalGuard.FindUnpayableEmployees(lines).ToArray());
        }

        [Fact]
        public void A_zero_net_is_payable_but_a_negative_one_is_not()
        {
            Assert.Empty(PayrollRunApprovalGuard.FindUnpayableEmployees(new[] { (7, 0m) }));
            Assert.Single(PayrollRunApprovalGuard.FindUnpayableEmployees(new[] { (7, -0.01m) }));
        }

        [Fact]
        public void An_empty_run_has_nothing_to_approve()
        {
            Assert.False(PayrollRunApprovalGuard.HasPayableContent(0));
            Assert.True(PayrollRunApprovalGuard.HasPayableContent(1));
        }
    }
}
