namespace Sms.Application.Dashboards
{
    /// <summary>BR-ATD-009's central computation, reused verbatim — same numbers the Attendance monitor screen would show for the same section/date.</summary>
    public class SectionAttendanceSnapshot
    {
        public int ScheduledCount { get; set; }

        public int AbsentCount { get; set; }

        public int ExemptedCount { get; set; }

        public decimal PresentPercent { get; set; }
    }
}
