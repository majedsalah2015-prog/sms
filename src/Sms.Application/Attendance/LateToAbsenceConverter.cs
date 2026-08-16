namespace Sms.Application.Attendance
{
    /// <summary>
    /// Pure BR-ATD-004 (e.g. "3 lates = 1 unexcused absence"). Doc's own
    /// open question #2 recommends shipping this **disabled by default**
    /// (transparency concerns) — built here as a reusable pure function,
    /// not wired into any automatic capture flow.
    /// </summary>
    public static class LateToAbsenceConverter
    {
        public static int ConvertedAbsences(int lateCount, int lateThreshold)
        {
            if (lateThreshold <= 0)
            {
                return 0;
            }

            return lateCount / lateThreshold;
        }
    }
}
