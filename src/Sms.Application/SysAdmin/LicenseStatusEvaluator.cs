using System;
using Sms.Domain.SysAdmin;

namespace Sms.Application.SysAdmin
{
    /// <summary>Pure BR-SYS-006: Active until expiry, then a grace window of read-only-pending degradation, then ReadOnly — never a hard data lockout (product ethics stance).</summary>
    public static class LicenseStatusEvaluator
    {
        public static LicenseStatus ComputeStatus(DateTime nowUtc, DateTime expiresAtUtc, int graceDays)
        {
            if (nowUtc < expiresAtUtc)
            {
                return LicenseStatus.Active;
            }

            var graceEndUtc = expiresAtUtc.AddDays(graceDays);
            return nowUtc < graceEndUtc ? LicenseStatus.Grace : LicenseStatus.ReadOnly;
        }
    }
}
