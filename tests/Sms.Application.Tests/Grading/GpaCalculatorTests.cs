using Sms.Application.Grading;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grading
{
    public class GpaCalculatorTests
    {
        [Fact]
        [BusinessRule("BR-GRA-007")]
        public void Weighted_average_across_offerings()
        {
            var results = new[]
            {
                new GpaCalculator.OfferingResult(4.0m, 5m),
                new GpaCalculator.OfferingResult(3.0m, 5m),
            };

            Assert.Equal(3.5m, GpaCalculator.Calculate(results));
        }

        [Fact]
        [BusinessRule("BR-GRA-007")]
        public void Offerings_without_gpa_points_are_excluded_not_zeroed()
        {
            var results = new[]
            {
                new GpaCalculator.OfferingResult(4.0m, 5m),
                new GpaCalculator.OfferingResult(null, 5m),
            };

            Assert.Equal(4.0m, GpaCalculator.Calculate(results));
        }

        [Fact]
        [BusinessRule("BR-GRA-007")]
        public void No_gpa_bearing_offerings_returns_zero()
        {
            var results = new[] { new GpaCalculator.OfferingResult(null, 5m) };

            Assert.Equal(0m, GpaCalculator.Calculate(results));
        }
    }
}
