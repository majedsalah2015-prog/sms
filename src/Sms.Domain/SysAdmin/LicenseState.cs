using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.SysAdmin
{
    /// <summary>
    /// core.LicenseState (BR-SYS-006, O5): one row per school. Status is
    /// derived by LicenseStatusEvaluator from Tier/StudentCountCap/
    /// ExpiresAtUtc/GraceDays against the current student count and clock —
    /// never set directly, so it can never drift from its inputs.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class LicenseState : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public LicenseTier Tier { get; set; }

        public int StudentCountCap { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public int GraceDays { get; set; } = 30;
    }
}
