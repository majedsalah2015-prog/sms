using System;
using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>Pure BR-SEC-004 session-expiry arithmetic; persistence is the service's job.</summary>
    public static class SessionPolicyEvaluator
    {
        public static int IdleTimeoutMinutes(AccountType accountType, SessionPolicy policy)
            => accountType == AccountType.Staff ? policy.StaffIdleTimeoutMinutes : policy.PortalIdleTimeoutMinutes;

        public static DateTime ComputeExpiresAtUtc(DateTime createdAtUtc, SessionPolicy policy)
            => createdAtUtc.AddHours(policy.AbsoluteTimeoutHours);

        /// <summary>True when the absolute ceiling has passed or the account type's idle window has elapsed.</summary>
        public static bool IsExpired(DateTime lastActivityAtUtc, DateTime expiresAtUtc, AccountType accountType, DateTime nowUtc, SessionPolicy policy)
        {
            if (nowUtc >= expiresAtUtc)
            {
                return true;
            }

            var idleLimit = lastActivityAtUtc.AddMinutes(IdleTimeoutMinutes(accountType, policy));
            return nowUtc >= idleLimit;
        }
    }
}
