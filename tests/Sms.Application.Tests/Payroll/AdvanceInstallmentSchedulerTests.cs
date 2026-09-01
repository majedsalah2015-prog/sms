using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Application.Payroll;
using Xunit;

namespace Sms.Application.Tests.Payroll
{
    /// <summary>
    /// Owner request, 2026-08-28. Deliberately untagged by <c>[BusinessRule]</c>: doc/Modules/12
    /// scopes payroll out (§2, BR-EMP-007) and describes no advances at all, so these rules have no
    /// numbered id to cite and inventing one would put a fabricated reference into the CI coverage
    /// gate. See <c>Sms.Domain.Payroll.PayrollRun</c> for the standing deviation.
    /// </summary>
    public class AdvanceInstallmentSchedulerTests
    {
        [Fact]
        public void Instalments_sum_to_the_advance_exactly_when_it_does_not_divide()
        {
            var schedule = AdvanceInstallmentScheduler.Build(1000m, 3, 2026, 9);

            Assert.Equal(1000m, schedule.Sum(i => i.Amount));
            Assert.Equal(new[] { 333.33m, 333.33m, 333.34m }, schedule.Select(i => i.Amount));
        }

        [Fact]
        public void The_remainder_lands_on_the_last_instalment_not_the_first()
        {
            var schedule = AdvanceInstallmentScheduler.Build(100m, 3, 2026, 1);

            Assert.Equal(33.33m, schedule.First().Amount);
            Assert.Equal(33.34m, schedule.Last().Amount);
        }

        [Fact]
        public void An_amount_that_divides_evenly_produces_equal_instalments()
        {
            var schedule = AdvanceInstallmentScheduler.Build(1200m, 4, 2026, 5);

            Assert.All(schedule, i => Assert.Equal(300m, i.Amount));
        }

        [Fact]
        public void Instalments_walk_forward_one_month_at_a_time_across_the_year_boundary()
        {
            var schedule = AdvanceInstallmentScheduler.Build(400m, 4, 2026, 11);

            Assert.Equal(
                new[] { (2026, 11), (2026, 12), (2027, 1), (2027, 2) },
                schedule.Select(i => (i.DueYear, i.DueMonth)));
        }

        [Fact]
        public void Sequence_numbers_start_at_one_and_run_in_due_order()
        {
            var schedule = AdvanceInstallmentScheduler.Build(600m, 6, 2026, 3);

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, schedule.Select(i => i.SequenceNo));
        }

        [Fact]
        public void A_single_instalment_carries_the_whole_advance()
        {
            var schedule = AdvanceInstallmentScheduler.Build(750.55m, 1, 2026, 9);

            var only = Assert.Single(schedule);
            Assert.Equal(750.55m, only.Amount);
            Assert.Equal(2026, only.DueYear);
            Assert.Equal(9, only.DueMonth);
        }

        // --- refusals ---------------------------------------------------------

        [Theory]
        [InlineData(0)]
        [InlineData(-250)]
        public void An_advance_for_nothing_is_refused(decimal amount)
        {
            Assert.Throws<InvalidAdvanceAmountException>(
                () => AdvanceInstallmentScheduler.Build(amount, 3, 2026, 9));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(61)]
        public void An_instalment_count_outside_the_ceiling_is_refused(int count)
        {
            Assert.Throws<InvalidAdvanceInstallmentCountException>(
                () => AdvanceInstallmentScheduler.Build(1000m, count, 2026, 9));
        }

        [Fact]
        public void A_split_that_would_produce_an_instalment_of_nothing_is_refused()
        {
            // Two cents over three months cannot be paid back a cent at a time.
            Assert.Throws<InvalidAdvanceInstallmentCountException>(
                () => AdvanceInstallmentScheduler.Build(0.02m, 3, 2026, 9));
        }

        [Fact]
        public void The_smallest_split_that_still_gives_every_month_a_cent_is_allowed()
        {
            var schedule = AdvanceInstallmentScheduler.Build(0.03m, 3, 2026, 9);

            Assert.Equal(0.03m, schedule.Sum(i => i.Amount));
            Assert.All(schedule, i => Assert.Equal(0.01m, i.Amount));
        }

        [Theory]
        [InlineData(2026, 0)]
        [InlineData(2026, 13)]
        [InlineData(1999, 6)]
        public void A_first_deduction_month_that_is_not_a_real_month_is_refused(int year, int month)
        {
            Assert.Throws<InvalidPayrollPeriodException>(
                () => AdvanceInstallmentScheduler.Build(1000m, 3, year, month));
        }

        [Fact]
        public void EnsureSchedulable_refuses_exactly_what_Build_refuses()
        {
            Assert.Throws<InvalidAdvanceAmountException>(
                () => AdvanceInstallmentScheduler.EnsureSchedulable(0m, 3, 2026, 9));
            Assert.Throws<InvalidAdvanceInstallmentCountException>(
                () => AdvanceInstallmentScheduler.EnsureSchedulable(1000m, 0, 2026, 9));
            Assert.Throws<InvalidPayrollPeriodException>(
                () => AdvanceInstallmentScheduler.EnsureSchedulable(1000m, 3, 2026, 13));
        }
    }
}
