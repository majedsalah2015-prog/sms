using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sms.Application.Dashboards;
using Xunit;

namespace Sms.Application.Tests.Dashboards
{
    public class ChartGeometryTests
    {
        // ------------------------------------------------------------------ axis

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1.5, 2)]
        [InlineData(2.4, 2.5)]
        [InlineData(3, 5)]
        [InlineData(7, 10)]
        [InlineData(12, 20)]
        [InlineData(3847, 5000)]
        [InlineData(210000, 250000)]
        public void NiceCeiling_rounds_up_to_a_readable_axis_top(decimal largest, decimal expected)
        {
            Assert.Equal(expected, ChartGeometry.NiceCeiling(largest));
        }

        [Fact]
        public void NiceCeiling_never_returns_zero_so_a_flat_chart_still_has_an_axis()
        {
            Assert.Equal(1m, ChartGeometry.NiceCeiling(0m));
            Assert.Equal(1m, ChartGeometry.NiceCeiling(-500m));
        }

        [Theory]
        [InlineData(0.4)]
        [InlineData(0.02)]
        [InlineData(0.999)]
        public void A_sub_unit_series_is_floored_at_one_rather_than_magnified(decimal largest)
        {
            // Every figure on this screen is a headcount or an amount of money. An
            // axis topping out at 0.5 is measuring half a person, and scaling that
            // to full height turns rounding dust into a mountain range.
            Assert.Equal(1m, ChartGeometry.NiceCeiling(largest));
        }

        [Fact]
        public void NiceCeiling_is_never_below_the_value_it_has_to_contain()
        {
            // The tallest bar sitting one pixel over the top of the plot is the whole
            // failure this rounds away from; a value that lands exactly on a step
            // must not be pushed to the next one either.
            foreach (var value in new[] { 1m, 2m, 2.5m, 5m, 10m, 99m, 100m, 101m, 1234.56m })
            {
                Assert.True(ChartGeometry.NiceCeiling(value) >= value, $"ceiling fell below {value}");
            }
        }

        [Fact]
        public void AxisTicks_runs_from_zero_to_the_ceiling_inclusive()
        {
            var ticks = ChartGeometry.AxisTicks(1000m, divisions: 4);

            Assert.Equal(new[] { 0m, 250m, 500m, 750m, 1000m }, ticks);
        }

        // ------------------------------------------------------------------ percentages

        [Fact]
        public void Percent_of_nothing_is_zero_rather_than_a_divide_by_zero()
        {
            Assert.Equal(0m, ChartGeometry.Percent(50m, 0m));
            Assert.Equal(0m, ChartGeometry.Percent(50m, -1m));
        }

        [Fact]
        public void BarPercent_clamps_rather_than_letting_a_bar_overhang_its_card()
        {
            Assert.Equal(100m, ChartGeometry.BarPercent(150m, 100m));
            Assert.Equal(0m, ChartGeometry.BarPercent(-5m, 100m));
        }

        // ------------------------------------------------------------------ donut

        [Fact]
        public void Donut_sweeps_add_up_to_a_whole_circle()
        {
            var slices = ChartGeometry.Donut(new[] { 1m, 1m, 1m }, 100m, 100m, 90m, 55m);

            // Thirds do not divide 360 in decimal, so the closing slice takes the
            // remainder — otherwise there is a visible seam at twelve o'clock.
            Assert.Equal(3, slices.Count);
            Assert.Equal(360m, slices.Sum(s => s.SweepAngle));
            Assert.Equal(0m, slices[0].StartAngle);
        }

        [Fact]
        public void Donut_skips_zero_values_but_keeps_the_index_of_the_ones_it_draws()
        {
            var slices = ChartGeometry.Donut(new[] { 5m, 0m, 5m }, 100m, 100m, 90m, 55m);

            Assert.Equal(2, slices.Count);
            Assert.Equal(new[] { 0, 2 }, slices.Select(s => s.Index));
        }

        [Fact]
        public void Donut_of_nothing_is_empty_so_the_caller_can_write_a_sentence_instead()
        {
            Assert.Empty(ChartGeometry.Donut(new[] { 0m, 0m }, 100m, 100m, 90m, 55m));
            Assert.Empty(ChartGeometry.Donut(Array.Empty<decimal>(), 100m, 100m, 90m, 55m));
        }

        [Fact]
        public void A_single_category_draws_a_full_ring_rather_than_nothing()
        {
            // One arc whose start and end coincide renders as nothing at all, and a
            // school with one payment method is the first case to hit it.
            var slices = ChartGeometry.Donut(new[] { 42m }, 100m, 100m, 90m, 55m);

            var slice = Assert.Single(slices);
            Assert.Equal(360m, slice.SweepAngle);
            Assert.Equal(100m, slice.Percent);

            // Two arcs each way — four A commands — is what makes the ring visible.
            Assert.Equal(4, slice.Path.Count(c => c == 'A'));
        }

        [Fact]
        public void A_half_slice_is_not_flagged_as_a_large_arc_but_a_bigger_one_is()
        {
            var half = ChartGeometry.Donut(new[] { 1m, 1m }, 100m, 100m, 90m, 55m)[0];
            var most = ChartGeometry.Donut(new[] { 3m, 1m }, 100m, 100m, 90m, 55m)[0];

            Assert.Contains("A 90 90 0 0 1", half.Path);
            Assert.Contains("A 90 90 0 1 1", most.Path);
        }

        [Fact]
        public void The_first_slice_starts_at_twelve_oclock()
        {
            var slice = ChartGeometry.Donut(new[] { 1m, 1m }, 100m, 100m, 90m, 55m)[0];

            // Centre (100,100), radius 90, angle 0 => straight up.
            Assert.StartsWith("M 100 10 ", slice.Path, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------ trend

        [Fact]
        public void Polyline_puts_the_ceiling_at_the_top_and_zero_on_the_floor()
        {
            var points = ChartGeometry.Polyline(new[] { 0m, 100m }, width: 200m, height: 50m, ceiling: 100m);

            // SVG's y axis grows downward: the biggest value is the smallest y.
            Assert.Equal("0,50 200,0", points);
        }

        [Fact]
        public void A_single_month_still_draws_a_line_rather_than_an_invisible_dot()
        {
            var points = ChartGeometry.Polyline(new[] { 50m }, width: 200m, height: 100m, ceiling: 100m);

            Assert.Equal("0,50 200,50", points);
        }

        [Fact]
        public void Polyline_of_nothing_is_empty()
        {
            Assert.Equal(string.Empty, ChartGeometry.Polyline(Array.Empty<decimal>(), 200m, 100m, 100m));
        }

        [Fact]
        public void Polyline_survives_a_zero_ceiling_instead_of_dividing_by_it()
        {
            var points = ChartGeometry.Polyline(new[] { 0m, 0m }, width: 100m, height: 40m, ceiling: 0m);

            Assert.Equal("0,40 100,40", points);
        }

        [Fact]
        public void AreaPath_opens_and_closes_on_the_baseline()
        {
            var path = ChartGeometry.AreaPath(new[] { 100m, 0m }, width: 200m, height: 50m, ceiling: 100m);

            Assert.Equal("M 0 50 L 0 0 L 200 50 L 200 50 Z", path);
        }

        [Fact]
        public void AreaPath_of_a_single_month_is_still_an_area()
        {
            var path = ChartGeometry.AreaPath(new[] { 50m }, width: 200m, height: 100m, ceiling: 100m);

            Assert.Equal("M 0 100 L 0 50 L 200 50 L 200 100 Z", path);
        }

        // ------------------------------------------------------------------ the reason this class exists

        [Theory]
        [InlineData("ar-SA")]
        [InlineData("ar-EG")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void Every_emitted_coordinate_is_invariant_whatever_the_ambient_culture(string culture)
        {
            // The failure this guards: a view under ar-SA or de-DE interpolating a
            // decimal writes "12,5" or worse into a path, and the browser draws an
            // empty box. It is invisible in English and total in the other language,
            // which is exactly the kind of bug that ships.
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);

                var donut = ChartGeometry.Donut(new[] { 1m, 2m, 3m }, 100m, 100m, 87.5m, 55.25m);
                var polyline = ChartGeometry.Polyline(new[] { 12.5m, 33.75m }, 200.5m, 50.25m, 100m);
                var area = ChartGeometry.AreaPath(new[] { 12.5m, 33.75m }, 200.5m, 50.25m, 100m);

                var emitted = string.Join(" ", donut.Select(s => s.Path)) + " " + polyline + " " + area;

                Assert.DoesNotContain(",", string.Join(" ", donut.Select(s => s.Path)), StringComparison.Ordinal);
                Assert.All(emitted, c => Assert.True(
                    char.IsDigit(c) && c <= '9' || "MALZ., -".Contains(c, StringComparison.Ordinal),
                    $"'{c}' (U+{(int)c:X4}) is not valid SVG path data"));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void Percentages_do_not_drift_with_the_ambient_culture_either()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
                Assert.Equal(33.3m, ChartGeometry.Percent(1m, 3m));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void Slice_percentages_are_each_slices_share_of_the_drawn_total()
        {
            var slices = ChartGeometry.Donut(new List<decimal> { 25m, 75m }, 100m, 100m, 90m, 55m);

            Assert.Equal(25m, slices[0].Percent);
            Assert.Equal(75m, slices[1].Percent);
        }
    }
}
