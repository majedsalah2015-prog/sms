using Sms.Application.Grades;
using Sms.Domain.Grades;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grades
{
    public class GenderPolicyNarrowingTests
    {
        [Theory]
        [InlineData(GenderPolicy.Mixed, GenderPolicy.Mixed)]
        [InlineData(GenderPolicy.Mixed, GenderPolicy.Boys)]
        [InlineData(GenderPolicy.Mixed, GenderPolicy.Girls)]
        [InlineData(GenderPolicy.Boys, GenderPolicy.Boys)]
        [InlineData(GenderPolicy.Girls, GenderPolicy.Girls)]
        [BusinessRule("BR-GRD-004")]
        public void Valid_narrowings_are_allowed(GenderPolicy stage, GenderPolicy requested)
        {
            Assert.True(GenderPolicyNarrowing.IsValidNarrowing(stage, requested));
        }

        [Theory]
        [InlineData(GenderPolicy.Boys, GenderPolicy.Mixed)]
        [InlineData(GenderPolicy.Boys, GenderPolicy.Girls)]
        [InlineData(GenderPolicy.Girls, GenderPolicy.Mixed)]
        [InlineData(GenderPolicy.Girls, GenderPolicy.Boys)]
        [BusinessRule("BR-GRD-004")]
        public void Widening_a_single_gender_stage_is_rejected(GenderPolicy stage, GenderPolicy requested)
        {
            Assert.False(GenderPolicyNarrowing.IsValidNarrowing(stage, requested));
        }
    }
}
