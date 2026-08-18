using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Setup
{
    /// <summary>
    /// Pure BR-SET-005/doc §9 engine for the Regional.WorkingDays setting:
    /// parses the DayOfWeek code list and enforces "≥ 4 working days". This
    /// closes the gap noted since E-103 (School had no weekend-day
    /// configuration): Calendar/Timetable/Installments callers that pass
    /// weekend days can now read them from here via
    /// <see cref="WeekendDays"/>.
    /// </summary>
    public static class WorkingWeek
    {
        public const int MinimumWorkingDays = 4;

        public static string? Validate(string value)
        {
            var codes = SettingKeys.SplitCodes(value);
            if (codes.Count == 0)
            {
                return "at least one working day";
            }

            var days = new HashSet<DayOfWeek>();
            foreach (var code in codes)
            {
                if (!Enum.TryParse<DayOfWeek>(code, true, out var day))
                {
                    return $"'{code}' is not a day of week";
                }

                days.Add(day);
            }

            return days.Count < MinimumWorkingDays
                ? $"working week must contain at least {MinimumWorkingDays} working days"
                : null;
        }

        public static IReadOnlyList<DayOfWeek> Parse(string value) =>
            SettingKeys.SplitCodes(value)
                .Select(c => Enum.Parse<DayOfWeek>(c, true))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

        public static string Format(IEnumerable<DayOfWeek> days) => string.Join(",", days.Distinct().OrderBy(d => d));

        /// <summary>The complement of the working days — what Calendar/Timetable treat as weekend.</summary>
        public static IReadOnlyList<DayOfWeek> WeekendDays(string workingDaysValue)
        {
            var working = Parse(workingDaysValue);
            return Enum.GetValues<DayOfWeek>().Where(d => !working.Contains(d)).ToList();
        }
    }
}
