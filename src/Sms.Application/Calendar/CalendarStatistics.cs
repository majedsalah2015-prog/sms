using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Calendar;

namespace Sms.Application.Calendar
{
    /// <summary>
    /// Pure BR-CAL-006 building block: the live instructional-day count.
    /// Comparing it against a ministry minimum needs country-pack config
    /// that doesn't exist yet — deferred; this just counts.
    /// </summary>
    public static class CalendarStatistics
    {
        public static int CountInstructionalDays(IEnumerable<DayType> dayTypes)
            => dayTypes.Count(d => d is DayType.Working or DayType.ExamPeriodWorking or DayType.Partial);
    }
}
