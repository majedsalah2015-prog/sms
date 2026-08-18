using System;

namespace Sms.Application.Audit
{
    /// <summary>Pure BR-AUM-002 out-of-hours-admin-action detector: flags actions outside the school's configured office-hours window (school-local time already resolved by the caller).</summary>
    public static class OutOfHoursActionDetector
    {
        public static bool IsOutOfHours(TimeSpan localTimeOfDay, TimeSpan officeHoursStart, TimeSpan officeHoursEnd)
            => localTimeOfDay < officeHoursStart || localTimeOfDay >= officeHoursEnd;
    }
}
