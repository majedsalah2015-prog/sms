using System;
using Sms.Application.Calendar;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Calendar
{
    public class CalendarChangeGuardTests
    {
        private static readonly DateTime Today = new(2026, 8, 15);

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public void A_date_before_today_is_a_past_date()
        {
            Assert.True(CalendarChangeGuard.IsPastDate(new DateTime(2026, 8, 14), Today));
        }

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public void Today_itself_is_not_a_past_date()
        {
            Assert.False(CalendarChangeGuard.IsPastDate(Today, Today));
        }

        [Fact]
        [BusinessRule("BR-CAL-004")]
        public void A_future_date_is_not_a_past_date()
        {
            Assert.False(CalendarChangeGuard.IsPastDate(new DateTime(2026, 8, 16), Today));
        }
    }
}
