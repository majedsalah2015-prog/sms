using System;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Security
{
    public class SessionPolicyEvaluatorTests
    {
        private static readonly DateTime CreatedAt = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
        private static readonly SessionPolicy Policy = SessionPolicy.Default; // staff 30m idle, portal 20m idle, 12h absolute

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public void A_fresh_session_is_not_expired()
        {
            var expiresAtUtc = SessionPolicyEvaluator.ComputeExpiresAtUtc(CreatedAt, Policy);

            Assert.False(SessionPolicyEvaluator.IsExpired(CreatedAt, expiresAtUtc, AccountType.Staff, CreatedAt, Policy));
        }

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public void Staff_sessions_idle_out_after_thirty_minutes()
        {
            var expiresAtUtc = SessionPolicyEvaluator.ComputeExpiresAtUtc(CreatedAt, Policy);
            var lastActivity = CreatedAt;
            var now = lastActivity.AddMinutes(31);

            Assert.True(SessionPolicyEvaluator.IsExpired(lastActivity, expiresAtUtc, AccountType.Staff, now, Policy));
        }

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public void Portal_sessions_idle_out_sooner_than_staff()
        {
            var expiresAtUtc = SessionPolicyEvaluator.ComputeExpiresAtUtc(CreatedAt, Policy);
            var now = CreatedAt.AddMinutes(21);

            Assert.True(SessionPolicyEvaluator.IsExpired(CreatedAt, expiresAtUtc, AccountType.Parent, now, Policy));
            Assert.False(SessionPolicyEvaluator.IsExpired(CreatedAt, expiresAtUtc, AccountType.Staff, now, Policy));
        }

        [Fact]
        [BusinessRule("BR-SEC-004")]
        public void The_absolute_ceiling_expires_the_session_even_with_continuous_activity()
        {
            var expiresAtUtc = SessionPolicyEvaluator.ComputeExpiresAtUtc(CreatedAt, Policy);
            var lastActivity = CreatedAt.AddHours(12).AddMinutes(-1); // active a minute ago
            var now = CreatedAt.AddHours(12).AddMinutes(1); // past the 12h absolute ceiling

            Assert.True(SessionPolicyEvaluator.IsExpired(lastActivity, expiresAtUtc, AccountType.Staff, now, Policy));
        }
    }
}
