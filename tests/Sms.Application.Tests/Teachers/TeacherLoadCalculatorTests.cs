using Sms.Application.Teachers;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Teachers
{
    public class TeacherLoadCalculatorTests
    {
        [Fact]
        [BusinessRule("BR-TCH-004")]
        public void CurrentLoad_sums_assigned_offering_periods()
        {
            Assert.Equal(11, TeacherLoadCalculator.CurrentLoad(new[] { 5, 4, 2 }));
        }

        [Fact]
        [BusinessRule("BR-TCH-004")]
        public void CurrentLoad_of_no_assignments_is_zero()
        {
            Assert.Equal(0, TeacherLoadCalculator.CurrentLoad(new int[0]));
        }

        [Theory]
        [InlineData(20, 4, 24, false)]
        [InlineData(20, 5, 24, true)]
        [InlineData(0, 25, 24, true)]
        [BusinessRule("BR-TCH-004")]
        public void ExceedsMax_is_true_only_when_total_would_pass_the_cap(int currentLoad, int additional, int max, bool expected)
        {
            Assert.Equal(expected, TeacherLoadCalculator.ExceedsMax(currentLoad, additional, max));
        }
    }
}
