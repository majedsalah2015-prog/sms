using Sms.Application.Timetable;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Timetable
{
    public class SubstituteEligibilityEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-TTB-007")]
        public void Free_and_qualified_is_eligible()
        {
            Assert.True(SubstituteEligibilityEvaluator.IsEligible(isFreeAtSlot: true, isQualified: true, allowSuperviseOnly: false));
        }

        [Fact]
        [BusinessRule("BR-TTB-007")]
        public void Free_but_unqualified_needs_supervise_only_flag()
        {
            Assert.False(SubstituteEligibilityEvaluator.IsEligible(isFreeAtSlot: true, isQualified: false, allowSuperviseOnly: false));
            Assert.True(SubstituteEligibilityEvaluator.IsEligible(isFreeAtSlot: true, isQualified: false, allowSuperviseOnly: true));
        }

        [Fact]
        [BusinessRule("BR-TTB-007")]
        public void Not_free_is_never_eligible_regardless_of_qualification()
        {
            Assert.False(SubstituteEligibilityEvaluator.IsEligible(isFreeAtSlot: false, isQualified: true, allowSuperviseOnly: true));
        }
    }
}
