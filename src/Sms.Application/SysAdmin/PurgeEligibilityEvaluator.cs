using System;

namespace Sms.Application.SysAdmin
{
    /// <summary>Pure BR-SYS-005 (and BR-AUM-005 for the audit-data case): a purge is eligible only once its horizon has passed, no legal hold is active over its data class, and (for audit data) no unresolved failed integrity verification has frozen maintenance.</summary>
    public static class PurgeEligibilityEvaluator
    {
        public static bool IsEligible(DateTime horizonUtc, DateTime nowUtc, bool hasActiveLegalHold, bool isAuditFrozen)
            => nowUtc >= horizonUtc && !hasActiveLegalHold && !isAuditFrozen;
    }
}
