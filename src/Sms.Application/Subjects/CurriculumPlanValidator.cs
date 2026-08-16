using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Subjects
{
    /// <summary>
    /// Pure BR-SUB-005/009 checks. The available-periods-per-week ceiling
    /// comes from the timetable shape (Module 15, doesn't exist yet) —
    /// callers supply it explicitly, same pattern as E-009's TimeZoneInfo
    /// and E-103's weekend-day set.
    /// </summary>
    public static class CurriculumPlanValidator
    {
        public static int TotalWeeklyPeriods(IEnumerable<int> offeringWeeklyPeriods)
            => offeringWeeklyPeriods.Sum();

        public static bool IsWithinAvailableSlots(int totalWeeklyPeriods, int availableSlotsPerWeek)
            => totalWeeklyPeriods <= availableSlotsPerWeek;

        /// <summary>BR-SUB §9: an assessable offering must carry a positive weight; a non-assessable one is unconstrained.</summary>
        public static bool HasValidWeight(bool isAssessable, decimal gpaWeight)
            => !isAssessable || gpaWeight > 0;
    }
}
