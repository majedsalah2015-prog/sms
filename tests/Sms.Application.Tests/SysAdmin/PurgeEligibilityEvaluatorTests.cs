using System;
using Sms.Application.SysAdmin;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.SysAdmin
{
    public class PurgeEligibilityEvaluatorTests
    {
        private static readonly DateTime Horizon = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        [BusinessRule("BR-SYS-005")]
        public void Eligible_once_horizon_passed_with_no_hold_or_freeze()
        {
            Assert.True(PurgeEligibilityEvaluator.IsEligible(Horizon, Horizon.AddDays(1), hasActiveLegalHold: false, isAuditFrozen: false));
        }

        [Fact]
        [BusinessRule("BR-SYS-005")]
        public void Not_eligible_before_the_horizon()
        {
            Assert.False(PurgeEligibilityEvaluator.IsEligible(Horizon, Horizon.AddDays(-1), hasActiveLegalHold: false, isAuditFrozen: false));
        }

        [Fact]
        [BusinessRule("BR-SYS-005")]
        public void Not_eligible_under_an_active_legal_hold()
        {
            Assert.False(PurgeEligibilityEvaluator.IsEligible(Horizon, Horizon.AddDays(1), hasActiveLegalHold: true, isAuditFrozen: false));
        }

        [Fact]
        [BusinessRule("BR-AUM-005")]
        public void Not_eligible_while_audit_maintenance_is_frozen()
        {
            Assert.False(PurgeEligibilityEvaluator.IsEligible(Horizon, Horizon.AddDays(1), hasActiveLegalHold: false, isAuditFrozen: true));
        }
    }
}
