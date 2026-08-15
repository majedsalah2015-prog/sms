using System;

namespace Sms.Application.Security
{
    /// <summary>Pure BR-SEC-002 lockout arithmetic; persistence is the service's job.</summary>
    public static class LockoutEvaluator
    {
        public static LockoutStatus Evaluate(int accessFailedCount, DateTime? lockedOutUntilUtc, DateTime nowUtc, LockoutPolicy policy)
        {
            var isLockedOut = lockedOutUntilUtc is DateTime until && until > nowUtc;

            return new LockoutStatus
            {
                IsLockedOut = isLockedOut,
                UnlocksAtUtc = isLockedOut ? lockedOutUntilUtc : null,
                RequiresCaptcha = accessFailedCount >= policy.CaptchaThreshold,
            };
        }

        /// <summary>
        /// Next (AccessFailedCount, LockedOutUntilUtc) after a failed attempt.
        /// Hitting the threshold locks out from this moment, resetting the
        /// counter so the next window starts clean once it expires.
        /// </summary>
        public static (int AccessFailedCount, DateTime? LockedOutUntilUtc) RegisterFailure(
            int accessFailedCountBefore, DateTime nowUtc, LockoutPolicy policy)
        {
            var count = accessFailedCountBefore + 1;
            if (count >= policy.FailureThreshold)
            {
                return (0, nowUtc.AddMinutes(policy.LockoutDurationMinutes));
            }

            return (count, null);
        }
    }
}
