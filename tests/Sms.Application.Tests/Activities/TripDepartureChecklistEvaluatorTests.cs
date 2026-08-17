using Sms.Application.Activities;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Activities
{
    public class TripDepartureChecklistEvaluatorTests
    {
        [Theory]
        [InlineData(true, true, true, true)]
        [InlineData(false, true, true, false)]
        [InlineData(true, false, true, false)]
        [InlineData(true, true, false, false)]
        [BusinessRule("BR-ACT-004")]
        public void CanDepart_requires_all_three(bool ratio, bool consents, bool transport, bool expected)
        {
            Assert.Equal(expected, TripDepartureChecklistEvaluator.CanDepart(ratio, consents, transport));
        }

        [Theory]
        [InlineData(20, 20, true)]
        [InlineData(20, 19, false)]
        [InlineData(20, 21, false)]
        [BusinessRule("BR-ACT-004")]
        public void HeadcountMatches_requires_an_exact_match(int departed, int returned, bool expected)
        {
            Assert.Equal(expected, TripDepartureChecklistEvaluator.HeadcountMatches(departed, returned));
        }
    }
}
