using System;
using System.Collections.Generic;

namespace Sms.Application.Dashboards
{
    /// <summary>
    /// The month axis a trend chart is laid onto: every calendar month an academic
    /// year touches, oldest first, whether anything happened in it or not.
    /// <para>
    /// Zero-filled by construction, and that is the whole reason it is not a
    /// <c>GroupBy</c> in a query. Grouping billed charges by month returns the
    /// months that <em>had</em> charges, so a school that billed in September and
    /// January draws a two-point line reading "September, January" — two adjacent
    /// columns implying a continuous rise, with the four quiet months in between
    /// silently deleted. The axis has to come from the year's dates and the data
    /// has to be dropped onto it.
    /// </para>
    /// </summary>
    public static class MonthAxisBuilder
    {
        /// <summary>
        /// An academic year is ten or eleven months; this tolerates a badly saved
        /// one without letting it render a chart two thousand columns wide.
        /// </summary>
        public const int MaximumMonths = 24;

        /// <summary>
        /// Every month from the one containing <paramref name="startDate"/> to the
        /// one containing <paramref name="endDate"/>, inclusive of both.
        /// <para>
        /// An end before the start yields the single month the start falls in
        /// rather than an empty axis: a year saved back to front is a data problem
        /// for the academic-year screen to refuse, and this screen's job meanwhile
        /// is to render something a reader can see is wrong, not to blank out.
        /// </para>
        /// </summary>
        public static IReadOnlyList<(int Year, int Month)> Build(DateTime startDate, DateTime endDate)
        {
            var months = new List<(int Year, int Month)>();
            var cursor = new DateTime(startDate.Year, startDate.Month, 1);
            var last = new DateTime(endDate.Year, endDate.Month, 1);

            do
            {
                months.Add((cursor.Year, cursor.Month));
                cursor = cursor.AddMonths(1);
            }
            while (cursor <= last && months.Count < MaximumMonths);

            return months;
        }

        /// <summary>
        /// How many months <see cref="Build"/> would return — what the ledger port
        /// needs, which counts months rather than listing them.
        /// </summary>
        public static int Count(DateTime startDate, DateTime endDate)
            => Build(startDate, endDate).Count;
    }
}
