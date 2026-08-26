using System;

namespace Sms.Application.Security
{
    /// <summary>
    /// BR-SEC-022's dormant-account rule: an account nobody has signed into for more than sixty days
    /// is dormant, and dormant accounts are a review queue rather than an automatic action.
    /// <para>
    /// An account that has never been signed into at all is measured from the day it was provisioned,
    /// not treated as dormant on its first afternoon. That distinction is the whole value of the
    /// queue in a school: the accounts worth chasing are the batch provisioned in September that
    /// nobody ever collected, and they are indistinguishable from a legitimate never-used account
    /// until the sixty days have actually passed.
    /// </para>
    /// <para>
    /// Deactivation is never automatic here. The doc calls this a quarterly cleanup queue precisely
    /// because a teacher on unpaid leave and a leaver whose offboarding was never recorded look the
    /// same to a clock, and only a person can tell them apart.
    /// </para>
    /// </summary>
    public static class AccountDormancy
    {
        /// <summary>BR-SEC-022: "no login &gt; 60 days".</summary>
        public const int DormantAfterDays = 60;

        /// <summary>
        /// Whether the account is dormant as of <paramref name="nowUtc"/>.
        /// <paramref name="lastSignInAtUtc"/> is null for an account never signed into, and
        /// <paramref name="provisionedAtUtc"/> stands in for it.
        /// </summary>
        public static bool IsDormant(
            DateTime? lastSignInAtUtc,
            DateTime provisionedAtUtc,
            DateTime nowUtc,
            int dormantAfterDays = DormantAfterDays)
            => DaysSinceUse(lastSignInAtUtc, provisionedAtUtc, nowUtc) > dormantAfterDays;

        /// <summary>
        /// Whole days since the account was last used — or, never having been used, since it was
        /// provisioned. Negative gaps (a clock skew, a seeded future date) are reported as zero
        /// rather than as a negative age nobody can read.
        /// </summary>
        public static int DaysSinceUse(DateTime? lastSignInAtUtc, DateTime provisionedAtUtc, DateTime nowUtc)
        {
            var since = lastSignInAtUtc ?? provisionedAtUtc;
            var days = (nowUtc - since).TotalDays;
            return days <= 0 ? 0 : (int)days;
        }
    }
}
