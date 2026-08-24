using System;
using System.Linq;
using Sms.Application.Classrooms;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Classrooms
{
    /// <summary>
    /// doc/Modules/08 §8.5. The heat map is an argument about facilities — "we are at
    /// 46%" is what turns "we need another room" into a claim — so the arithmetic
    /// behind it is tested for the cases that would quietly inflate or deflate it.
    /// </summary>
    public class RoomUtilizationCalculatorTests
    {
        private static RoomUtilizationCalculator.TeachingSlot[] Week(int count)
            => Enumerable.Range(1, count)
                .Select(i => new RoomUtilizationCalculator.TeachingSlot(i, DayOfWeek.Sunday, i))
                .ToArray();

        [Fact]
        [BusinessRule("BR-ROM-005")]
        public void A_room_placed_in_half_the_week_reads_as_half_used()
        {
            var rows = RoomUtilizationCalculator.Build(
                new[] { 7 },
                Week(6),
                new[]
                {
                    new RoomUtilizationCalculator.RoomPlacement(7, 1),
                    new RoomUtilizationCalculator.RoomPlacement(7, 2),
                    new RoomUtilizationCalculator.RoomPlacement(7, 3),
                });

            var row = rows.Single();
            Assert.Equal(3, row.OccupiedSlots);
            Assert.Equal(6, row.TeachingSlots);
            Assert.Equal(50, row.PercentUsed);
        }

        /// <summary>
        /// An empty row is the most useful line on this screen for whoever is deciding
        /// where to put a class, so a room nothing is placed in still appears.
        /// </summary>
        [Fact]
        public void A_room_nothing_is_placed_in_still_has_a_row()
        {
            var rows = RoomUtilizationCalculator.Build(new[] { 1, 2 }, Week(4), Array.Empty<RoomUtilizationCalculator.RoomPlacement>());

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(0, r.PercentUsed));
        }

        /// <summary>
        /// A placement in a slot outside the week's shape — a stale period from an
        /// earlier shape — must not count, or a room reads as over 100% used.
        /// </summary>
        [Fact]
        public void A_placement_in_a_slot_the_week_no_longer_has_is_ignored()
        {
            var rows = RoomUtilizationCalculator.Build(
                new[] { 7 },
                Week(2),
                new[]
                {
                    new RoomUtilizationCalculator.RoomPlacement(7, 1),
                    new RoomUtilizationCalculator.RoomPlacement(7, 99),
                });

            Assert.Equal(1, rows.Single().OccupiedSlots);
            Assert.Equal(50, rows.Single().PercentUsed);
        }

        /// <summary>
        /// Two classes in one room in one period is a clash the validator refuses, but
        /// a published version can still carry one where the room was assigned after
        /// validation. The slot counts once towards occupancy — it is one period of
        /// room time — and the clash is surfaced rather than smoothed away.
        /// </summary>
        [Fact]
        [BusinessRule("BR-ROM-005")]
        public void A_double_booking_counts_once_but_is_reported()
        {
            var rows = RoomUtilizationCalculator.Build(
                new[] { 7 },
                Week(4),
                new[]
                {
                    new RoomUtilizationCalculator.RoomPlacement(7, 1),
                    new RoomUtilizationCalculator.RoomPlacement(7, 1),
                });

            var row = rows.Single();
            Assert.Equal(1, row.OccupiedSlots);
            Assert.Equal(25, row.PercentUsed);
            Assert.True(row.HasDoubleBooking);
            Assert.Equal(2, row.BySlot[1]);
        }

        [Fact]
        public void The_overall_figure_is_room_periods_used_over_room_periods_available()
        {
            var rows = RoomUtilizationCalculator.Build(
                new[] { 1, 2 },
                Week(4),
                new[]
                {
                    new RoomUtilizationCalculator.RoomPlacement(1, 1),
                    new RoomUtilizationCalculator.RoomPlacement(1, 2),
                    new RoomUtilizationCalculator.RoomPlacement(2, 1),
                });

            // 3 of 8 room-periods.
            Assert.Equal(38, RoomUtilizationCalculator.OverallPercent(rows));
        }

        [Fact]
        public void A_week_with_no_teaching_periods_reads_zero_rather_than_dividing_by_it()
        {
            var rows = RoomUtilizationCalculator.Build(
                new[] { 1 },
                Array.Empty<RoomUtilizationCalculator.TeachingSlot>(),
                new[] { new RoomUtilizationCalculator.RoomPlacement(1, 1) });

            Assert.Equal(0, rows.Single().PercentUsed);
            Assert.Equal(0, RoomUtilizationCalculator.OverallPercent(rows));
        }
    }
}
