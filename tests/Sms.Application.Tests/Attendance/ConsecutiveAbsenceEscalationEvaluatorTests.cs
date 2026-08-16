using Sms.Application.Attendance;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Attendance
{
    public class ConsecutiveAbsenceEscalationEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-ATD-008")]
        public void LongestUnexcusedStreak_finds_the_longest_run()
        {
            var days = new[] { true, true, false, true, true, true, false, true };

            Assert.Equal(3, ConsecutiveAbsenceEscalationEvaluator.LongestUnexcusedStreak(days));
        }

        [Fact]
        [BusinessRule("BR-ATD-008")]
        public void No_absences_gives_a_zero_streak()
        {
            var days = new[] { false, false, false };

            Assert.Equal(0, ConsecutiveAbsenceEscalationEvaluator.LongestUnexcusedStreak(days));
        }

        [Theory]
        [InlineData(3, true)]
        [InlineData(4, false)]
        [BusinessRule("BR-ATD-008")]
        public void ShouldEscalate_compares_the_longest_streak_to_the_threshold(int threshold, bool expected)
        {
            var days = new[] { true, true, true, false };

            Assert.Equal(expected, ConsecutiveAbsenceEscalationEvaluator.ShouldEscalate(days, threshold));
        }
    }
}
