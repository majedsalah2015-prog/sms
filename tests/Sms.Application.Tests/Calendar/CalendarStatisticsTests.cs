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
    }
}
