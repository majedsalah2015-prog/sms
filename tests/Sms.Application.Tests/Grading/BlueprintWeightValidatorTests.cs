using Sms.Application.Grading;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grading
{
    public class BlueprintWeightValidatorTests
    {
        [Fact]
        [BusinessRule("BR-GRA-003")]
        public void Weights_summing_to_exactly_100_are_valid()
        {
            Assert.True(BlueprintWeightValidator.SumsTo100(new[] { 30m, 30m, 40m }));
        }

        [Theory]
        [BusinessRule("BR-GRA-003")]
        [InlineData(99)]
        [InlineData(101)]
        public void Weights_not_summing_to_100_are_invalid(decimal total)
        {
            Assert.False(BlueprintWeightValidator.SumsTo100(new[] { total }));
        }
    }
}
