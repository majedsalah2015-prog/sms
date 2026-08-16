namespace Sms.Application.Attendance
{
    /// <summary>
    /// Pure BR-ATD-009: the single, centrally-defined attendance %
    /// computation every consumer (report cards, certificates, ministry
    /// reports — none built yet) must use rather than recomputing its own.
    /// </summary>
    public static class AttendancePercentageCalculator
    {
        public static decimal Calculate(int scheduledDays, int exemptedDays, int absentDays)
        {
            var baseDays = scheduledDays - exemptedDays;
            if (baseDays <= 0)
            {
                return 100m;
            }

            var presentDays = baseDays - absentDays;
            return (decimal)presentDays / baseDays * 100m;
        }
    }
}
