using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-CAL-004: mid-year changes to past dates are blocked outright.</summary>
    public class CalendarPastDateEditException : InvalidOperationException
    {
        public CalendarPastDateEditException(DateTime date)
            : base($"Cannot edit the calendar for {date:yyyy-MM-dd} — it is in the past (BR-CAL-004).")
        {
        }
    }

    /// <summary>BR-GLB-051: every calendar day/event must fall within its academic year.</summary>
    public class CalendarDateOutsideYearException : InvalidOperationException
    {
        public CalendarDateOutsideYearException(DateTime date)
            : base($"{date:yyyy-MM-dd} falls outside the academic year's date range (BR-GLB-051).")
        {
        }
    }
}
