using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.TestSupport;
using Sms.Web.Finance;
using Sms.Web.Navigation;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The payments register (doc/Modules/21 §10), pinned where it is wrong in ways nobody notices:
    /// a range that silently drops its own last day, and an attribution that quietly loses money.
    /// <para>
    /// Both failures produce a plausible number. A month register missing the 31st still prints a
    /// total, and a receipt whose unallocated remainder is dropped still prints a total — they are
    /// simply the wrong totals, on the screen a school reconciles its bank account against. That is
    /// the class of defect a rendering check cannot catch, so it is held here instead.
    /// </para>
    /// </summary>
    public class PaymentRegisterTests
    {
        // ---------------------------------------------------------------- the range

        /// <summary>
        /// The default a finance office is asked for: this month so far. Not "the last 30 days",
        /// which straddles two months and answers nobody's question at month end.
        /// </summary>
        [Fact]
        public void An_empty_filter_reads_the_month_the_clock_is_in()
        {
            var (from, to) = PaymentRegister.Range(null, null, new DateTime(2026, 3, 17, 9, 30, 0));

            Assert.Equal(new DateTime(2026, 3, 1), from);
            Assert.Equal(new DateTime(2026, 3, 17), to);
        }

        /// <summary>
        /// The whole of the end date is inside the register. Comparing <c>IssuedAtUtc &lt;= to</c>
        /// against a midnight <c>DateTime</c> loses every receipt taken during that day's business —
        /// a full day of collection, and the figure still looks like a total.
        /// </summary>
        [Fact]
        [BusinessRule("BR-PAY-002")]
        public void The_last_day_of_the_range_is_included_to_its_final_second()
        {
            var to = new DateTime(2026, 3, 31);
            var boundary = PaymentRegister.EndExclusive(to);

            Assert.True(new DateTime(2026, 3, 31, 0, 0, 0) < boundary, "a receipt at midnight on the end date is inside the range");
            Assert.True(new DateTime(2026, 3, 31, 14, 05, 0) < boundary, "an afternoon receipt on the end date is inside the range");
            Assert.True(new DateTime(2026, 3, 31, 23, 59, 59) < boundary, "the last second of the end date is inside the range");
            Assert.False(new DateTime(2026, 4, 1, 0, 0, 0) < boundary, "the next day is outside it");
        }

        /// <summary>
        /// A reversed pair is a typo, not a request for an empty register — and an empty register
        /// reads as "the school collected nothing", which is a worse answer than the one intended.
        /// </summary>
        [Fact]
        public void A_backwards_range_is_read_the_way_round_it_was_meant()
        {
            var (from, to) = PaymentRegister.Range(
                new DateTime(2026, 3, 31), new DateTime(2026, 3, 1), new DateTime(2026, 5, 5));

            Assert.Equal(new DateTime(2026, 3, 1), from);
            Assert.Equal(new DateTime(2026, 3, 31), to);
        }

        /// <summary>A time-of-day left on either bound must not shift the range by part of a day.</summary>
        [Fact]
        public void The_bounds_are_dates_whatever_time_arrives_on_them()
        {
            var (from, to) = PaymentRegister.Range(
                new DateTime(2026, 3, 1, 18, 45, 0), new DateTime(2026, 3, 31, 6, 15, 0), new DateTime(2026, 5, 5));

            Assert.Equal(new DateTime(2026, 3, 1), from);
            Assert.Equal(new DateTime(2026, 3, 31), to);
        }

        // ---------------------------------------------------------------- the attribution

        /// <summary>
        /// BR-PAY-003: one receipt, oldest-first across everything the payer owes. A parent paying
        /// for two children is two rows, and each row carries what actually reached that child —
        /// not the receipt's face value repeated twice, which would double the register's total.
        /// </summary>
        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void A_receipt_spread_over_two_siblings_is_two_rows_that_add_back_to_it()
        {
            var lines = PaymentRegister.Split(1000m, new[]
            {
                (StudentId: 7, Amount: 300m),
                (StudentId: 9, Amount: 500m),
                (StudentId: 7, Amount: 200m),
            });

            Assert.Equal(2, lines.Count);
            Assert.Equal(500m, lines.Single(l => l.StudentId == 7).Amount);
            Assert.Equal(500m, lines.Single(l => l.StudentId == 9).Amount);
            Assert.Equal(1000m, lines.Sum(l => l.Amount));
        }

        /// <summary>
        /// BR-PAY-003's leftover: money the engine could not put against an invoice is the payer's
        /// credit balance. It belongs to no child, so it gets a row of its own rather than being
        /// attributed to whichever child happened to be billed first.
        /// </summary>
        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void What_the_allocation_could_not_place_is_a_row_belonging_to_no_child()
        {
            var lines = PaymentRegister.Split(1000m, new[] { (StudentId: 7, Amount: 600m) });

            Assert.Equal(2, lines.Count);
            Assert.Equal(600m, lines.Single(l => l.StudentId == 7).Amount);
            Assert.Equal(400m, lines.Single(l => l.StudentId == null).Amount);
            Assert.Equal(1000m, lines.Sum(l => l.Amount));
        }

        /// <summary>An advance payment against nothing at all is still the whole receipt, on one unattributed row.</summary>
        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void A_receipt_that_paid_no_invoice_is_one_unattributed_row_for_its_whole_value()
        {
            var lines = PaymentRegister.Split(750m, Array.Empty<(int, decimal)>());

            var only = Assert.Single(lines);
            Assert.Null(only.StudentId);
            Assert.Equal(750m, only.Amount);
        }

        /// <summary>
        /// A fully allocated receipt must not grow a zero "unallocated" row. It would put an empty
        /// line under every receipt in a month's register, and a screen that noisy stops being read.
        /// </summary>
        [Fact]
        public void A_fully_allocated_receipt_has_no_leftover_row()
        {
            var lines = PaymentRegister.Split(500m, new[] { (StudentId: 3, Amount: 500m) });

            var only = Assert.Single(lines);
            Assert.Equal(3, only.StudentId);
            Assert.Equal(500m, only.Amount);
        }

        /// <summary>
        /// The property the footer depends on: whatever the allocation looks like, the rows of a
        /// receipt add back to the receipt. Without it the register cannot be reconciled against the
        /// day's takings, which is the only reason anybody opens it.
        /// </summary>
        [Theory]
        [InlineData(1000, 0, 0)]
        [InlineData(1000, 1000, 0)]
        [InlineData(1000, 250, 250)]
        [InlineData(1000, 999, 1)]
        [BusinessRule("BR-PAY-003")]
        public void The_rows_of_a_receipt_always_add_back_to_the_receipt(int amount, int first, int second)
        {
            var allocations = new List<(int, decimal)>();
            if (first > 0) allocations.Add((1, first));
            if (second > 0) allocations.Add((2, second));

            var lines = PaymentRegister.Split(amount, allocations);

            Assert.Equal(amount, lines.Sum(l => l.Amount));
        }

        // ---------------------------------------------------------------- the way in

        /// <summary>
        /// The screen is reachable, from both places this product offers a screen from: the finance
        /// sub-navigation and the P-LAUNCH finance workspace. A register nobody can find is the same
        /// as one that was never built.
        /// </summary>
        [Fact]
        public void The_register_is_offered_by_the_finance_workspace()
        {
            var finance = Assert.Single(WorkspaceCatalog.Workspaces, w => w.Key == "finance");

            var link = Assert.Single(finance.Links, l => l.ScreenCode == ScreenCatalog.Payments.Register);
            Assert.Equal(ScreenCatalog.Modules.Payments, link.ModuleCode);
            Assert.Equal("Payments", link.Controller);
            Assert.Equal("Register", link.Action);
            Assert.Equal("سجل الدفعات", link.TitleAr);
        }

        /// <summary>
        /// BR-SEC-021: the file is its own right. Reading the register on screen and carrying the
        /// school's collection out of the building are two different permissions, and the catalogue
        /// is where that separation has to exist for an administrator to be able to grant one.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SEC-021")]
        public void Reading_the_register_and_exporting_it_are_separate_rights()
        {
            var screen = Assert.Single(
                ScreenCatalog.Screens,
                s => s.ModuleCode == ScreenCatalog.Modules.Payments && s.ScreenCode == ScreenCatalog.Payments.Register);

            Assert.Contains(ActionVerb.View, screen.Verbs);
            Assert.Contains(ActionVerb.Export, screen.Verbs);
            Assert.Equal("سجل الدفعات", screen.TitleAr);
        }
    }
}
