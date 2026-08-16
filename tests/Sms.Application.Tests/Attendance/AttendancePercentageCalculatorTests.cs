using Sms.Application.Attendance;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Attendance
{
    public class AttendancePercentageCalculatorTests
    {
        [Theory]
        [InlineData(180, 0, 0, 100)]
        [InlineData(180, 0, 18, 90)]
        [InlineData(180, 0, 180, 0)]
        [InlineData(180, 30, 15, 90)] // base = 150, present = 135 -> 90%
        [BusinessRule("BR-ATD-009")]
        public void Calculate_uses_scheduled_minus_exempted_as_the_base(int scheduled, int exempted, int absent, decimal expected)
        {
            Assert.Equal(expected, AttendancePercentageCalculator.Calculate(scheduled, exempted, absent));
        }

        [Fact]
        [BusinessRule("BR-ATD-009")]
        public void Fully_exempted_base_does_not_divide_by_zero()
        {
            Assert.Equal(100m, AttendancePercentageCalculator.Calculate(scheduledDays: 10, exemptedDays: 10, absentDays: 0));
        }
    }
}
