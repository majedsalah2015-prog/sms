using System;

namespace Sms.Application.SysAdmin
{
    /// <summary>Pure BR-SYS-007: a maintenance banner needs a minimum lead time before its window starts, except for emergency read-only toggles.</summary>
    public static class MaintenanceLeadTimeGuard
    {
        public static bool HasSufficientLeadTime(DateTime nowUtc, DateTime windowStartUtc, TimeSpan minimumLeadTime, bool isEmergency)
            => isEmergency || windowStartUtc - nowUtc >= minimumLeadTime;
    }
}
