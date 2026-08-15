using System;
using System.Collections.Generic;
using Sms.Application.Calendar;
using Sms.Domain.Calendar;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Calendar
{
    public class CalendarDayResolverTests
    {
        private static readonly ISet<DayOfWeek> KsaWeekend = new HashSet<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday };

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public void A_weekday_with_no_override_resolves_to_Working()
        {
            var monday = new DateTime(2026, 8, 17); // a Monday
            Assert.Equal(DayType.Working, CalendarDayResolver.Resolve(monday, KsaWeekend, new Dictionary<DateTime, DayType>()));
        }

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public void A_configured_weekend_day_resolves_to_Weekend()
        {
            var friday = new DateTime(2026, 8, 21);
            Assert.Equal(DayType.Weekend, CalendarDayResolver.Resolve(friday, KsaWeekend, new Dictionary<DateTime, DayType>()));
        }

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public void A_manual_override_wins_over_the_weekend_rule_a_makeup_working_Saturday()
        {
            var saturday = new DateTime(2026, 8, 22);
            var overrides = new Dictionary<DateTime, DayType> { [saturday] = DayType.Working };

            Assert.Equal(DayType.Working, CalendarDayResolver.Resolve(saturday, KsaWeekend, overrides));
        }

        [Fact]
        [BusinessRule("BR-CAL-001")]
        public void A_manual_override_wins_over_a_regular_weekday_a_holiday()
        {
            var monday = new DateTime(2026, 8, 17);
            var overrides = new Dictionary<DateTime, DayType> { [monday] = DayType.Holiday };

            Assert.Equal(DayType.Holiday, CalendarDayResolver.Resolve(monday, KsaWeekend, overrides));
        }
    }
}
