using Sms.Domain.Grading;

namespace Sms.Application.Grading
{
    /// <summary>Pure BR-GRA-006: overall pass mark + max failed-subjects gate. Per-subject minimums and makeup-exam gates (BR-EXM-008) aren't modeled in this slice — flagged deferred, same as PromotionCriteria's own scope note.</summary>
    public static class PromotionEvaluator
    {
        public static PromotionOutcome Evaluate(int failedSubjectCount, int maxFailedSubjectsForPromotion, bool overallPassed)
        {
            if (!overallPassed)
            {
                return PromotionOutcome.Retain;
            }

            if (failedSubjectCount == 0)
            {
                return PromotionOutcome.Promote;
            }

            return failedSubjectCount <= maxFailedSubjectsForPromotion ? PromotionOutcome.Conditional : PromotionOutcome.Retain;
        }
    }
}
