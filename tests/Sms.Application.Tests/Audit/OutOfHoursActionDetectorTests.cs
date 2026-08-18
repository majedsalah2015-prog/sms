using System;
using Sms.Application.Audit;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Audit
{
    public class OutOfHoursActionDetectorTests
    {
        [Theory]
        [InlineData(6, 0, true)]
        [InlineData(7, 0, false)]
        [InlineData(15, 59, false)]
        [InlineData(16, 0, true)]
        [InlineData(23, 0, true)]
        [InlineData(3, 0, true)]
        [BusinessRule("BR-AUM-002")]
        public void Flags_actions_outside_the_office_hours_window(int hour, int minute, bool expected)
        {
            var start = TimeSpan.FromHours(7);
            var end = TimeSpan.FromHours(16);

            Assert.Equal(expected, OutOfHoursActionDetector.IsOutOfHours(new TimeSpan(hour, minute, 0), start, end));
        }
    }
}
