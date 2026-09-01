using System;
using System.Collections.Generic;
using Sms.Application.Installments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Installments
{
    /// <summary>
    /// The case these came from: a student billed 3,100 (2,250 tuition on a nine-month plan,
    /// 850 of uniform and materials on none) with one 250 receipt against the tuition. The
    /// screen read "المستحق 2,850" — the whole year — on the same page as the schedule that
    /// said otherwise.
    /// </summary>
    public class ScheduledPositionSplitterTests
    {
        private static readonly DateTime Today = new(2026, 9, 1);

        private static ScheduledPositionSplitter.ScheduledAmount At(string due, decimal amount, decimal covered = 0m, bool superseded = false, bool writtenOff = false)
            => new(DateTime.Parse(due), amount, covered, superseded, writtenOff);

        [Fact]
        [BusinessRule("BR-INS-007")]
        public void A_schedule_moves_the_future_installments_out_of_todays_claim()
        {
            var schedule = new List<ScheduledPositionSplitter.ScheduledAmount>
            {
                At("2026-09-01", 249.98m, 249.98m),
                At("2026-10-04", 249.98m, 0.02m),
                At("2026-11-08", 249.98m),
                At("2026-12-10", 249.98m),
                At("2027-01-13", 249.98m),
                At("2027-02-15", 249.98m),
                At("2027-03-21", 249.98m),
                At("2027-04-25", 249.98m),
                At("2027-05-27", 250.16m),
            };

            var position = ScheduledPositionSplitter.Split(2850m, schedule, Today);

            // The 850 that is on no schedule, and nothing else: the first installment is settled.
            Assert.Equal(850m, position.DueNow);
            Assert.Equal(2000m, position.NotYetDue);
            Assert.Equal(850m, position.Unscheduled);
            Assert.Equal(2850m, position.Total);
        }

        [Fact]
        [BusinessRule("BR-FEE-008")]
        public void With_no_schedule_the_whole_balance_is_payable_on_demand()
        {
            var position = ScheduledPositionSplitter.Split(3100m, Array.Empty<ScheduledPositionSplitter.ScheduledAmount>(), Today);

            Assert.Equal(3100m, position.DueNow);
            Assert.Equal(3100m, position.Unscheduled);
            Assert.Equal(0m, position.NotYetDue);
            Assert.False(position.DefersAnything);
        }

        [Fact]
        [BusinessRule("BR-INS-007")]
        public void An_installment_due_today_is_due_now_and_tomorrows_is_not()
        {
            var schedule = new List<ScheduledPositionSplitter.ScheduledAmount> { At("2026-09-01", 500m), At("2026-09-02", 500m) };

            var position = ScheduledPositionSplitter.Split(1000m, schedule, Today);

            Assert.Equal(500m, position.DueNow);
            Assert.Equal(500m, position.NotYetDue);
            Assert.Equal(0m, position.Unscheduled);
        }

        [Fact]
        [BusinessRule("BR-INS-005")]
        public void Superseded_installments_are_history_not_a_claim()
        {
            // The reschedule kept the old rows and wrote new ones; counting both would double the debt.
            var schedule = new List<ScheduledPositionSplitter.ScheduledAmount>
            {
                At("2026-08-01", 1000m, superseded: true),
                At("2026-12-01", 1000m),
            };

            var position = ScheduledPositionSplitter.Split(1000m, schedule, Today);

            Assert.Equal(0m, position.DueNow);
            Assert.Equal(1000m, position.NotYetDue);
        }

        [Fact]
        [BusinessRule("BR-INS-010")]
        public void A_written_off_installment_is_neither_due_now_nor_deferred()
        {
            var schedule = new List<ScheduledPositionSplitter.ScheduledAmount>
            {
                At("2026-08-01", 400m, writtenOff: true),
                At("2026-08-15", 300m),
                At("2026-12-01", 300m),
            };

            var position = ScheduledPositionSplitter.Split(1000m, schedule, Today);

            Assert.Equal(300m, position.DueNow);
            Assert.Equal(300m, position.NotYetDue);
            Assert.Equal(400m, position.WrittenOff);
            Assert.Equal(1000m, position.Total);
        }

        [Fact]
        [BusinessRule("BR-INS-003")]
        public void A_schedule_that_outruns_the_balance_gives_way_from_its_tail()
        {
            // A credit note reduced the charge and nobody called ReduceScheduleAsync. BR-INS-003
            // reduces future installments first, so the drift is absorbed the same way.
            var schedule = new List<ScheduledPositionSplitter.ScheduledAmount> { At("2026-08-01", 500m), At("2026-10-01", 500m), At("2026-12-01", 500m) };

            var position = ScheduledPositionSplitter.Split(700m, schedule, Today);

            Assert.Equal(500m, position.DueNow);
            Assert.Equal(200m, position.NotYetDue);
            Assert.Equal(700m, position.Total);
        }

        [Fact]
        [BusinessRule("BR-FEE-008")]
        public void A_charge_added_after_the_plan_is_payable_on_demand()
        {
            var schedule = new List<ScheduledPositionSplitter.ScheduledAmount> { At("2027-01-01", 1000m) };

            var position = ScheduledPositionSplitter.Split(1300m, schedule, Today);

            Assert.Equal(300m, position.DueNow);
            Assert.Equal(300m, position.Unscheduled);
            Assert.Equal(1000m, position.NotYetDue);
        }

        [Fact]
        [BusinessRule("BR-FEE-008")]
        public void A_payer_in_credit_is_reported_whole_and_never_deferred()
        {
            var schedule = new List<ScheduledPositionSplitter.ScheduledAmount> { At("2027-01-01", 1000m, 1000m) };

            var position = ScheduledPositionSplitter.Split(-120m, schedule, Today);

            Assert.Equal(-120m, position.DueNow);
            Assert.Equal(0m, position.NotYetDue);
            Assert.False(position.DefersAnything);
        }
    }
}
