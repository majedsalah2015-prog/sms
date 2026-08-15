using System;
using System.Collections.Generic;
using Sms.Domain.Calendar;

namespace Sms.Application.Calendar
{
    /// <summary>
    /// Pure BR-CAL-001: a manual override wins; otherwise the school's
    /// working-week configuration decides Weekend vs Working. Consumers
    /// (Attendance, Timetable) call this instead of re-deriving week logic.
    /// No School/SchoolSetting entity carries the working-week config yet
    /// (S1 remaining work) — callers supply it explicitly for now, same
    /// pattern as E-009's SchoolTimeZoneConverter taking a TimeZoneInfo.
    /// </summary>
    public static class CalendarDayResolver
    {
        public static DayType Resolve(DateTime date, ISet<DayOfWeek> weekendDays, IReadOnlyDictionary<DateTime, DayType> manualOverrides)
        {
            if (manualOverrides.TryGetValue(date.Date, out var overrideType))
            {
                return overrideType;
            }

            return weekendDays.Contains(date.DayOfWeek) ? DayType.Weekend : DayType.Working;
        }
    }
}
