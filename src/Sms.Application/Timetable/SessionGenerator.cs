using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Timetable
{
    /// <summary>Pure BR-TTB-006: dated Session instances = the published pattern x working days in range. Working-day determination is supplied by the caller (reuses E-103's CalendarDayResolver against real CalendarDay/School config) so this stays a pure function.</summary>
    public static class SessionGenerator
    {
        public readonly struct PlacementSlot
        {
            public PlacementSlot(int placementId, DayOfWeek dayOfWeek)
            {
                PlacementId = placementId;
                DayOfWeek = dayOfWeek;
            }

            public int PlacementId { get; }

            public DayOfWeek DayOfWeek { get; }
        }

        public static IEnumerable<(int PlacementId, DateTime Date)> Generate(
            DateTime rangeStart, DateTime rangeEnd, IEnumerable<PlacementSlot> placements, Func<DateTime, bool> isWorkingDay)
        {
            var placementList = placements.ToList();
            for (var date = rangeStart.Date; date <= rangeEnd.Date; date = date.AddDays(1))
            {
                if (!isWorkingDay(date))
                {
                    continue;
                }

                foreach (var placement in placementList.Where(p => p.DayOfWeek == date.DayOfWeek))
                {
                    yield return (placement.PlacementId, date);
                }
            }
        }
    }
}
