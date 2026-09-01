using System;
using System.Linq;
using Sms.Application.Dashboards;
using Xunit;

namespace Sms.Application.Tests.Dashboards
{
    public class MonthAxisBuilderTests
    {
        [Fact]
        public void An_academic_year_spans_every_month_it_touches()
        {
            var months = MonthAxisBuilder.Build(new DateTime(2025, 9, 1), new DateTime(2026, 6, 30));

            Assert.Equal(10, months.Count);
            Assert.Equal((2025, 9), months[0]);
            Assert.Equal((2026, 6), months[^1]);
        }

        [Fact]
        public void Quiet_months_are_on_the_axis_too()
        {
            // The whole reason this is not a GroupBy: a school that billed in
            // September and January must not draw a two-point line that deletes the
            // four quiet months between them and implies a continuous rise.
            var months = MonthAxisBuilder.Build(new DateTime(2025, 9, 15), new DateTime(2026, 1, 3));

            Assert.Equal(
                new[] { (2025, 9), (2025, 10), (2025, 11), (2025, 12), (2026, 1) },
                months.ToArray());
        }

        [Fact]
        public void A_day_inside_a_month_counts_as_the_whole_month()
        {
            var months = MonthAxisBuilder.Build(new DateTime(2025, 9, 30), new DateTime(2025, 9, 1));

            // Same month either way round — and see below for why a reversed range
            // does not come back empty.
            Assert.Equal(new[] { (2025, 9) }, months.ToArray());
        }

        [Fact]
        public void A_year_saved_back_to_front_yields_one_month_rather_than_none()
        {
            // A chart that blanks out tells the reader nothing; one that shows a
            // single stub month tells them something is wrong with the year.
            var months = MonthAxisBuilder.Build(new DateTime(2026, 6, 30), new DateTime(2025, 9, 1));

            Assert.Equal(new[] { (2026, 6) }, months.ToArray());
        }

        [Fact]
        public void A_wildly_long_range_is_capped_rather_than_drawing_a_mile_wide_chart()
        {
            var months = MonthAxisBuilder.Build(new DateTime(2000, 1, 1), new DateTime(2030, 1, 1));

            Assert.Equal(MonthAxisBuilder.MaximumMonths, months.Count);
        }

        [Fact]
        public void Count_agrees_with_Build()
        {
            var from = new DateTime(2025, 8, 20);
            var to = new DateTime(2026, 7, 5);

            Assert.Equal(MonthAxisBuilder.Build(from, to).Count, MonthAxisBuilder.Count(from, to));
        }
    }
}
