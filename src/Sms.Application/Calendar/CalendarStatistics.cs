using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Calendar;

namespace Sms.Application.Calendar
{
    /// <summary>
    /// BR-CAL-006: the live instructional-day count, and the comparison
    /// against the ministry minimum the school configures.
    /// </summary>
    public static class CalendarStatistics
    {
        public static int CountInstructionalDays(IEnumerable<DayType> dayTypes)
            => dayTypes.Count(d => d is DayType.Working or DayType.ExamPeriodWorking or DayType.Partial);

        /// <summary>
        /// BR-CAL-006's second half. The rule reads "activation warning (not block) when below",
        /// so this answers a question the screen asks — it never refuses anything.
        /// <para>
        /// A null or non-positive minimum is "the school has not configured one" and warns about
        /// nothing: doc/Modules/04 §14 Q1 leaves the per-country ministry values open, so the
        /// number is entered as a setting (<c>Regional.MinimumInstructionalDays</c>) rather than
        /// shipped as reference data, and an unset one must not manufacture a warning on every
        /// calendar in the product.
        /// </para>
        /// </summary>
        public static bool IsBelowMinimum(int instructionalDays, int? minimumInstructionalDays)
            => minimumInstructionalDays is int minimum && minimum > 0 && instructionalDays < minimum;

        /// <summary>How many instructional days short of the minimum, or 0 when it is met or unset.</summary>
        public static int ShortfallFromMinimum(int instructionalDays, int? minimumInstructionalDays)
            => IsBelowMinimum(instructionalDays, minimumInstructionalDays) ? minimumInstructionalDays!.Value - instructionalDays : 0;
    }
}
