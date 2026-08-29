using Sms.Application.Calendar;
using Sms.Domain.Calendar;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Calendar
{
    public class CalendarStatisticsTests
    {
        [Fact]
        [BusinessRule("BR-CAL-006")]
        public void Working_ExamPeriod_and_Partial_days_all_count_as_instructional()
        {
            var days = new[] { DayType.Working, DayType.ExamPeriodWorking, DayType.Partial };
            Assert.Equal(3, CalendarStatistics.CountInstructionalDays(days));
        }

        [Fact]
        [BusinessRule("BR-CAL-006")]
        public void Weekends_and_holidays_do_not_count()
        {
            var days = new[] { DayType.Weekend, DayType.Holiday, DayType.Working };
            Assert.Equal(1, CalendarStatistics.CountInstructionalDays(days));
        }

        // --- BR-CAL-006 ministry minimum ---------------------------------------

        [Fact]
        [BusinessRule("BR-CAL-006")]
        public void A_count_under_the_configured_minimum_warns()
        {
            Assert.True(CalendarStatistics.IsBelowMinimum(178, 180));
            Assert.Equal(2, CalendarStatistics.ShortfallFromMinimum(178, 180));
        }

        [Fact]
        [BusinessRule("BR-CAL-006")]
        public void Meeting_the_minimum_exactly_does_not_warn()
        {
            Assert.False(CalendarStatistics.IsBelowMinimum(180, 180));
            Assert.Equal(0, CalendarStatistics.ShortfallFromMinimum(180, 180));
        }

        [Fact]
        [BusinessRule("BR-CAL-006")]
        public void An_unconfigured_minimum_warns_about_nothing()
        {
            // doc/Modules/04 §14 Q1 leaves the per-country ministry values open, so most schools
            // will run with this unset. A null minimum manufacturing a warning on every calendar
            // in the product would train everyone to ignore the one that matters.
            Assert.False(CalendarStatistics.IsBelowMinimum(0, null));
            Assert.Equal(0, CalendarStatistics.ShortfallFromMinimum(0, null));
        }

        [Fact]
        [BusinessRule("BR-CAL-006")]
        public void A_zero_minimum_is_treated_as_unconfigured()
        {
            Assert.False(CalendarStatistics.IsBelowMinimum(0, 0));
        }
    }
}
