using Sms.Application.Attendance;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Attendance
{
    public class LateToAbsenceConverterTests
    {
        [Theory]
        [InlineData(9, 3, 3)]
        [InlineData(8, 3, 2)]
        [InlineData(2, 3, 0)]
        [InlineData(5, 0, 0)]
        [BusinessRule("BR-ATD-004")]
        public void ConvertedAbsences_is_integer_division_by_the_threshold(int lateCount, int threshold, int expected)
        {
            Assert.Equal(expected, LateToAbsenceConverter.ConvertedAbsences(lateCount, threshold));
        }
    }
}
