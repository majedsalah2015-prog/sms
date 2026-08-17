using System;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-INS-004: a due date on a non-working day shifts to the next
    /// working day (BR-CAL-003 default policy). The working-day predicate
    /// is supplied by the caller (CalendarDayResolver over the school's
    /// weekend days + CalendarDay overrides — same shape as
    /// TimetableAdmin's SessionGenerator usage), so this stays pure.
    /// </summary>
    public static class DueDateShifter
    {
        public static DateTime ShiftToWorkingDay(DateTime date, Func<DateTime, bool> isWorkingDay, int maxLookaheadDays = 14)
        {
            var candidate = date.Date;
            for (var i = 0; i <= maxLookaheadDays; i++)
            {
                if (isWorkingDay(candidate))
                {
                    return candidate;
                }

                candidate = candidate.AddDays(1);
            }

            throw new InvalidOperationException($"No working day found within {maxLookaheadDays} days of {date:yyyy-MM-dd} (BR-INS-004).");
        }
    }
}
