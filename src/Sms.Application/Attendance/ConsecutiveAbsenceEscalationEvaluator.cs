using System.Collections.Generic;

namespace Sms.Application.Attendance
{
    /// <summary>
    /// Pure partial coverage of BR-ATD-008's "consecutive absences >= N ->
    /// escalation" threshold. Built and tested but not wired to any
    /// EscalationCase persistence or notification dispatch — same
    /// "engine built, not wired" precedent as PromotionPathValidator
    /// (E-103) and AgeEligibilityEvaluator's first callers. Same-day
    /// notification, cumulative-%-based warning letters, and ministry
    /// truancy thresholds (the rest of BR-ATD-008) are deferred entirely.
    /// </summary>
    public static class ConsecutiveAbsenceEscalationEvaluator
    {
        public static int LongestUnexcusedStreak(IReadOnlyList<bool> isUnexcusedAbsentByDayInOrder)
        {
            var longest = 0;
            var current = 0;
            foreach (var isAbsent in isUnexcusedAbsentByDayInOrder)
            {
                current = isAbsent ? current + 1 : 0;
                if (current > longest)
                {
                    longest = current;
                }
            }

            return longest;
        }

        public static bool ShouldEscalate(IReadOnlyList<bool> isUnexcusedAbsentByDayInOrder, int consecutiveThreshold)
            => LongestUnexcusedStreak(isUnexcusedAbsentByDayInOrder) >= consecutiveThreshold;
    }
}
