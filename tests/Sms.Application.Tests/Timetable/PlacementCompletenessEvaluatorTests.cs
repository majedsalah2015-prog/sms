using Sms.Application.Timetable;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Timetable
{
    public class PlacementCompletenessEvaluatorTests
    {
        [Theory]
        [InlineData(5, 5, true)]
        [InlineData(4, 5, false)]
        [InlineData(6, 5, false)]
        [BusinessRule("BR-TTB-003")]
        public void IsComplete_requires_an_exact_match(int placed, int weekly, bool expected)
        {
            Assert.Equal(expected, PlacementCompletenessEvaluator.IsComplete(placed, weekly));
        }

        [Fact]
        [BusinessRule("BR-TTB-003")]
        public void Shortfall_is_the_remaining_periods_needed()
        {
            Assert.Equal(2, PlacementCompletenessEvaluator.Shortfall(3, 5));
        }
    }
}
