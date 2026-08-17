using Sms.Application.Grading;
using Sms.Domain.Grading;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grading
{
    public class PromotionEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-GRA-006")]
        public void Passed_with_no_failures_promotes()
        {
            Assert.Equal(PromotionOutcome.Promote, PromotionEvaluator.Evaluate(failedSubjectCount: 0, maxFailedSubjectsForPromotion: 2, overallPassed: true));
        }

        [Fact]
        [BusinessRule("BR-GRA-006")]
        public void Passed_with_failures_within_the_cap_is_conditional()
        {
            Assert.Equal(PromotionOutcome.Conditional, PromotionEvaluator.Evaluate(failedSubjectCount: 2, maxFailedSubjectsForPromotion: 2, overallPassed: true));
        }

        [Fact]
        [BusinessRule("BR-GRA-006")]
        public void Passed_but_failures_beyond_the_cap_retains()
        {
            Assert.Equal(PromotionOutcome.Retain, PromotionEvaluator.Evaluate(failedSubjectCount: 3, maxFailedSubjectsForPromotion: 2, overallPassed: true));
        }

        [Fact]
        [BusinessRule("BR-GRA-006")]
        public void Overall_failure_always_retains_regardless_of_subject_count()
        {
            Assert.Equal(PromotionOutcome.Retain, PromotionEvaluator.Evaluate(failedSubjectCount: 0, maxFailedSubjectsForPromotion: 2, overallPassed: false));
        }
    }
}
