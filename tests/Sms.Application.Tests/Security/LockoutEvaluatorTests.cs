using System;
using Sms.Application.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Security
{
    public class LockoutEvaluatorTests
    {
        private static readonly DateTime Now = new(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        private static readonly LockoutPolicy Policy = LockoutPolicy.Default; // threshold 5, 15 min, captcha at 3

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public void Below_threshold_failures_do_not_lock_the_account()
        {
            var (count, lockedOutUntil) = LockoutEvaluator.RegisterFailure(3, Now, Policy);

            Assert.Equal(4, count);
            Assert.Null(lockedOutUntil);
        }

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public void The_threshold_failure_locks_the_account_and_resets_the_counter()
        {
            var (count, lockedOutUntil) = LockoutEvaluator.RegisterFailure(4, Now, Policy);

            Assert.Equal(0, count);
            Assert.Equal(Now.AddMinutes(15), lockedOutUntil);
        }

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public void An_account_locked_in_the_future_reports_locked_out()
        {
            var status = LockoutEvaluator.Evaluate(0, Now.AddMinutes(5), Now, Policy);

            Assert.True(status.IsLockedOut);
            Assert.Equal(Now.AddMinutes(5), status.UnlocksAtUtc);
        }

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public void The_lockout_window_expires_on_its_own_without_an_explicit_unlock()
        {
            var status = LockoutEvaluator.Evaluate(0, Now.AddMinutes(-1), Now, Policy);

            Assert.False(status.IsLockedOut);
        }

        [Fact]
        [BusinessRule("BR-SEC-002")]
        public void Captcha_is_signalled_from_the_configured_failure_count()
        {
            Assert.False(LockoutEvaluator.Evaluate(2, null, Now, Policy).RequiresCaptcha);
            Assert.True(LockoutEvaluator.Evaluate(3, null, Now, Policy).RequiresCaptcha);
        }
    }
}
