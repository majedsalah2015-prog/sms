using System;
using System.Collections.Generic;
using Sms.Application.Installments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Installments
{
    /// <summary>
    /// doc/Modules/20 §8.5 / §10's collection window, and the two rules a school
    /// will one day dispute: what counts as falling due inside a range, and what
    /// may be chased once it does (BR-INS-009).
    /// </summary>
    public class OutstandingWindowEvaluatorTests
    {
        private static DueItem Item(
            string dueDate, decimal amount = 1000m, decimal settled = 0m, bool pdc = false, bool collectible = true,
            DueItemSource source = DueItemSource.Installment)
            => new(source, DateTime.Parse(dueDate), amount, settled, pdc, collectible);

        // ------------------------------------------------------------------ the window

        [Theory]
        [InlineData("2027-02-28", false)]
        [InlineData("2027-03-01", true)]
        [InlineData("2027-03-15", true)]
        [InlineData("2027-03-31", true)]
        [InlineData("2027-04-01", false)]
        public void Both_ends_of_the_window_are_included(string dueDate, bool expected)
        {
            var included = OutstandingWindowEvaluator.Includes(
                DateTime.Parse(dueDate), new DateTime(2027, 3, 1), new DateTime(2027, 3, 31));

            Assert.Equal(expected, included);
        }

        [Fact]
        public void A_missing_bound_leaves_that_end_open()
        {
            var due = new DateTime(2020, 1, 1);

            Assert.True(OutstandingWindowEvaluator.Includes(due, null, new DateTime(2027, 3, 31)));
            Assert.True(OutstandingWindowEvaluator.Includes(due, null, null));
            Assert.False(OutstandingWindowEvaluator.Includes(due, new DateTime(2027, 3, 1), null));
        }

        [Fact]
        public void The_time_of_day_never_moves_an_item_out_of_its_window()
        {
            // A charge is posted with a real timestamp, and the last day of a window would otherwise
            // exclude everything posted after midnight on it.
            var lateOnTheLastDay = new DateTime(2027, 3, 31, 23, 45, 0);

            Assert.True(OutstandingWindowEvaluator.Includes(lateOnTheLastDay, new DateTime(2027, 3, 1), new DateTime(2027, 3, 31)));
        }

        [Fact]
        public void A_backwards_window_is_refused_rather_than_answered_with_an_empty_list()
        {
            Assert.False(OutstandingWindowEvaluator.IsWindowValid(new DateTime(2027, 3, 31), new DateTime(2027, 3, 1)));
            Assert.True(OutstandingWindowEvaluator.IsWindowValid(new DateTime(2027, 3, 1), new DateTime(2027, 3, 31)));
            Assert.True(OutstandingWindowEvaluator.IsWindowValid(new DateTime(2027, 3, 1), new DateTime(2027, 3, 1)));
            Assert.True(OutstandingWindowEvaluator.IsWindowValid(null, null));
        }

        // ------------------------------------------------------------------ what is left

        [Fact]
        public void An_overpaid_item_owes_nothing_rather_than_less_than_nothing()
        {
            // BR-PAY-003 allocates oldest-first across everything a payer owes, so a receipt can
            // settle one item past its own value. Without the floor that surplus would silently pay
            // down a different installment in the window and under-state the family's arrears.
            var overpaid = Item("2027-03-10", amount: 1000m, settled: 1400m);
            var unpaid = Item("2027-03-20", amount: 1000m);

            Assert.Equal(0m, OutstandingWindowEvaluator.Outstanding(overpaid));

            var position = OutstandingWindowEvaluator.Position(new[] { overpaid, unpaid }, null, null);
            Assert.Equal(1000m, position.Outstanding);
            Assert.Equal(1, position.ItemCount);
        }

        [Theory]
        [InlineData(0, 1000)]
        [InlineData(250, 750)]
        [InlineData(1000, 0)]
        public void Outstanding_is_the_amount_less_what_has_settled_it(decimal settled, decimal expected)
            => Assert.Equal(expected, OutstandingWindowEvaluator.Outstanding(Item("2027-03-10", 1000m, settled)));

        [Fact]
        public void A_superseded_or_written_off_item_is_owed_nothing_and_counted_nowhere()
        {
            var replaced = Item("2027-03-10", collectible: false);

            Assert.Equal(0m, OutstandingWindowEvaluator.Outstanding(replaced));

            var position = OutstandingWindowEvaluator.Position(new[] { replaced }, null, null);
            Assert.Equal(0, position.ItemCount);
            Assert.Equal(0m, position.Outstanding);
            Assert.Null(position.OldestDueDate);
        }

        // ------------------------------------------------------------------ what may be chased

        [Fact]
        [BusinessRule("BR-INS-009")]
        public void A_cheque_covered_item_is_owed_but_never_chased()
        {
            var covered = Item("2027-03-10", amount: 1000m, pdc: true);

            Assert.False(OutstandingWindowEvaluator.IsNotifiable(covered));
            Assert.Equal(1000m, OutstandingWindowEvaluator.Outstanding(covered));
        }

        [Fact]
        [BusinessRule("BR-INS-009")]
        public void The_chaseable_total_excludes_only_the_cheque_covered_part()
        {
            var position = OutstandingWindowEvaluator.Position(
                new[] { Item("2027-03-10", 1000m, pdc: true), Item("2027-03-20", 600m) }, null, null);

            Assert.Equal(1600m, position.Outstanding);
            Assert.Equal(600m, position.Notifiable);
            Assert.True(position.HasPdcCoveredItems);
        }

        [Fact]
        [BusinessRule("BR-INS-009")]
        public void Nothing_with_a_zero_balance_is_chased()
        {
            Assert.False(OutstandingWindowEvaluator.IsNotifiable(Item("2027-03-10", 1000m, settled: 1000m)));
            Assert.False(OutstandingWindowEvaluator.IsNotifiable(Item("2027-03-10", collectible: false)));
            Assert.True(OutstandingWindowEvaluator.IsNotifiable(Item("2027-03-10", 1000m, settled: 999m)));
        }

        // ------------------------------------------------------------------ the position

        [Fact]
        public void The_position_totals_only_the_unpaid_items_inside_the_window()
        {
            var items = new List<DueItem>
            {
                Item("2027-02-10", 500m),                     // before the window
                Item("2027-03-05", 1000m, settled: 400m),     // in, partly paid
                Item("2027-03-25", 800m),                     // in
                Item("2027-03-28", 300m, settled: 300m),      // in, fully settled
                Item("2027-04-10", 900m),                     // after the window
            };

            var position = OutstandingWindowEvaluator.Position(items, new DateTime(2027, 3, 1), new DateTime(2027, 3, 31));

            Assert.Equal(2, position.ItemCount);
            Assert.Equal(1800m, position.Due);
            Assert.Equal(1400m, position.Outstanding);
            Assert.Equal(1400m, position.Notifiable);
            Assert.False(position.HasPdcCoveredItems);
        }

        [Fact]
        public void The_oldest_unpaid_due_date_is_what_the_aging_bucket_is_taken_from()
        {
            // Deliberately out of order, and with a settled item that is older than both — a paid
            // installment must not age the family as though it were still owed.
            var items = new[]
            {
                Item("2027-03-25", 800m),
                Item("2027-01-05", 500m, settled: 500m),
                Item("2027-03-05", 1000m),
            };

            var position = OutstandingWindowEvaluator.Position(items, null, null);

            Assert.Equal(new DateTime(2027, 3, 5), position.OldestDueDate);
        }

        [Fact]
        public void A_family_owing_nothing_in_the_window_reports_a_flat_zero()
        {
            var position = OutstandingWindowEvaluator.Position(
                new[] { Item("2027-01-05", 500m) }, new DateTime(2027, 3, 1), new DateTime(2027, 3, 31));

            Assert.Equal(0, position.ItemCount);
            Assert.Equal(0m, position.Due);
            Assert.Equal(0m, position.Outstanding);
            Assert.Equal(0m, position.Notifiable);
            Assert.Null(position.OldestDueDate);
            Assert.False(position.HasPdcCoveredItems);
        }

        [Fact]
        public void Scheduled_installments_and_unscheduled_charges_total_together()
        {
            // A family part-way through adopting installment plans owes both kinds at once, and the
            // roll must add them up rather than pick a side.
            var position = OutstandingWindowEvaluator.Position(
                new[]
                {
                    Item("2027-03-05", 1000m),
                    Item("2027-03-06", 250m, source: DueItemSource.UnscheduledCharge),
                },
                new DateTime(2027, 3, 1), new DateTime(2027, 3, 31));

            Assert.Equal(2, position.ItemCount);
            Assert.Equal(1250m, position.Outstanding);
        }

        [Fact]
        public void An_empty_or_null_item_list_is_a_zero_position_not_a_crash()
        {
            foreach (var position in new[]
            {
                OutstandingWindowEvaluator.Position(null!, null, null),
                OutstandingWindowEvaluator.Position(Array.Empty<DueItem>(), null, null),
            })
            {
                Assert.Equal(0, position.ItemCount);
                Assert.Equal(0m, position.Outstanding);
            }
        }
    }
}
