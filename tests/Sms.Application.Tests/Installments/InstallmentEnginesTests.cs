using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Installments;
using Sms.Domain.Installments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Installments
{
    public class InstallmentPaymentWaterfallTests
    {
        [Fact]
        [BusinessRule("BR-INS-007")]
        public void Collected_money_fills_the_earliest_installments_first()
        {
            var paid = InstallmentPaymentWaterfall.Apply(new[] { 300m, 300m, 400m }, 450m);

            Assert.Equal(new[] { 300m, 150m, 0m }, paid);
        }

        [Fact]
        [BusinessRule("BR-INS-007")]
        public void Overpayment_never_exceeds_the_schedule()
        {
            var paid = InstallmentPaymentWaterfall.Apply(new[] { 100m, 100m }, 500m);

            Assert.Equal(new[] { 100m, 100m }, paid);
        }
    }

    public class DueDateShifterTests
    {
        private static readonly HashSet<DayOfWeek> KsaWeekend = new() { DayOfWeek.Friday, DayOfWeek.Saturday };

        [Fact]
        [BusinessRule("BR-INS-004")]
        public void A_weekend_due_date_shifts_to_the_next_working_day()
        {
            var friday = new DateTime(2027, 1, 1); // 2027-01-01 is a Friday

            var shifted = DueDateShifter.ShiftToWorkingDay(friday, d => !KsaWeekend.Contains(d.DayOfWeek));

            Assert.Equal(new DateTime(2027, 1, 3), shifted);
            Assert.Equal(DayOfWeek.Sunday, shifted.DayOfWeek);
        }

        [Fact]
        [BusinessRule("BR-INS-004")]
        public void A_working_day_is_left_alone()
        {
            var monday = new DateTime(2027, 1, 4);

            Assert.Equal(monday, DueDateShifter.ShiftToWorkingDay(monday, d => !KsaWeekend.Contains(d.DayOfWeek)));
        }
    }

    public class ScheduleReductionAllocatorTests
    {
        private static readonly DateTime Today = new(2027, 1, 15);

        private static ScheduleReductionAllocator.OpenInstallment Open(int index, string due, decimal amount, decimal paid = 0m)
            => new(index, DateTime.Parse(due), amount, paid);

        [Fact]
        [BusinessRule("BR-INS-003")]
        public void Reduction_hits_future_installments_first_latest_first()
        {
            var installments = new[]
            {
                Open(1, "2026-12-01", 300m), Open(2, "2027-02-01", 300m), Open(3, "2027-04-01", 300m),
            };

            var changes = ScheduleReductionAllocator.Reduce(installments, 400m, Today);

            Assert.Equal(0m, changes[3]);
            Assert.Equal(200m, changes[2]);
            Assert.False(changes.ContainsKey(1));
        }

        [Fact]
        [BusinessRule("BR-INS-003")]
        public void Reduction_then_falls_back_to_past_due_last_to_first_but_never_below_the_paid_amount()
        {
            var installments = new[]
            {
                Open(1, "2026-10-01", 300m, paid: 300m), Open(2, "2026-12-01", 300m, paid: 100m), Open(3, "2027-02-01", 300m),
            };

            var changes = ScheduleReductionAllocator.Reduce(installments, 400m, Today);

            Assert.Equal(0m, changes[3]);
            Assert.Equal(200m, changes[2]);
            Assert.False(changes.ContainsKey(1));
        }

        [Fact]
        [BusinessRule("BR-INS-003")]
        public void Reducing_more_than_the_unpaid_remainder_is_rejected()
        {
            var installments = new[] { Open(1, "2027-02-01", 300m, paid: 100m) };

            Assert.Throws<InvalidOperationException>(() => ScheduleReductionAllocator.Reduce(installments, 250m, Today));
        }
    }

    public class RescheduleApprovalRouterTests
    {
        private static readonly DateTime YearEnd = new(2027, 6, 30);

        [Fact]
        [BusinessRule("BR-INS-005")]
        public void A_short_extension_within_the_year_stays_at_P3()
        {
            Assert.False(RescheduleApprovalRouter.RequiresPrincipal(new DateTime(2027, 2, 1), new DateTime(2027, 4, 1), YearEnd, maxExtensionMonths: 3));
        }

        [Fact]
        [BusinessRule("BR-INS-005")]
        public void Beyond_N_months_or_crossing_year_end_escalates_to_Principal()
        {
            Assert.True(RescheduleApprovalRouter.RequiresPrincipal(new DateTime(2027, 2, 1), new DateTime(2027, 5, 2), YearEnd, maxExtensionMonths: 3));
            Assert.True(RescheduleApprovalRouter.RequiresPrincipal(new DateTime(2027, 6, 1), new DateTime(2027, 7, 1), YearEnd, maxExtensionMonths: 3));
        }
    }

    public class DunningLadderEvaluatorTests
    {
        private static readonly DateTime Due = new(2027, 2, 1);
        private static readonly DunningStep[] None = Array.Empty<DunningStep>();

        [Fact]
        [BusinessRule("BR-INS-008")]
        public void Reminder_D7_fires_a_week_before_due()
        {
            var step = DunningLadderEvaluator.Next(Due, Due.AddDays(-7), InstallmentStatus.Scheduled, false, false, None, false);

            Assert.Equal(DunningStep.ReminderD7, step);
        }

        [Fact]
        [BusinessRule("BR-INS-008")]
        public void Only_the_latest_eligible_unfired_step_fires_per_run()
        {
            var step = DunningLadderEvaluator.Next(Due, Due.AddDays(20), InstallmentStatus.Overdue, true, false, None, false);

            Assert.Equal(DunningStep.Overdue14, step);
        }

        [Fact]
        [BusinessRule("BR-INS-008")]
        public void A_step_already_reached_does_not_fire_again()
        {
            var step = DunningLadderEvaluator.Next(Due, Due.AddDays(20), InstallmentStatus.Overdue, true, false, new[] { DunningStep.Overdue14 }, false);

            Assert.Null(step);
        }

        [Fact]
        [BusinessRule("BR-INS-008")]
        public void Overdue_notices_need_truly_overdue_status_not_just_a_past_due_date()
        {
            // Within grace: past due date, not yet truly overdue - and reminders don't fire after due date.
            var step = DunningLadderEvaluator.Next(Due, Due.AddDays(3), InstallmentStatus.Due, false, false, None, false);

            Assert.Null(step);
        }

        [Fact]
        [BusinessRule("BR-INS-009")]
        public void PDC_coverage_suppresses_the_whole_ladder()
        {
            var step = DunningLadderEvaluator.Next(Due, Due.AddDays(20), InstallmentStatus.Overdue, true, isPdcCovered: true, None, false);

            Assert.Null(step);
        }

        [Fact]
        [BusinessRule("BR-INS-006")]
        public void A_broken_promise_advances_to_the_next_step_immediately()
        {
            var step = DunningLadderEvaluator.Next(Due, Due.AddDays(5), InstallmentStatus.Overdue, true, false, new[] { DunningStep.Overdue3 }, hasBrokenPromise: true);

            Assert.Equal(DunningStep.Overdue14, step);
        }

        [Fact]
        [BusinessRule("BR-INS-008")]
        public void Nothing_fires_on_paid_or_closed_installments()
        {
            Assert.Null(DunningLadderEvaluator.Next(Due, Due.AddDays(40), InstallmentStatus.Paid, false, false, None, false));
            Assert.Null(DunningLadderEvaluator.Next(Due, Due.AddDays(40), InstallmentStatus.Rescheduled, false, false, None, false));
            Assert.Null(DunningLadderEvaluator.Next(Due, Due.AddDays(40), InstallmentStatus.WrittenOff, false, false, None, false));
        }

        [Fact]
        [BusinessRule("BR-INS-008")]
        public void The_ladder_ends_at_the_escalation_flag_stage()
        {
            var all = DunningLadderEvaluator.ProposedOffsetsDays.Keys.ToList();

            Assert.Null(DunningLadderEvaluator.Next(Due, Due.AddDays(400), InstallmentStatus.Overdue, true, false, all, false));
        }
    }
}
