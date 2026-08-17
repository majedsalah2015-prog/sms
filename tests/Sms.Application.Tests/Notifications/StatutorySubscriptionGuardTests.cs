using Sms.Application.Notifications;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Notifications
{
    public class StatutorySubscriptionGuardTests
    {
        [Fact]
        [BusinessRule("BR-NTF-002")]
        public void Non_statutory_rules_can_always_be_disabled()
        {
            Assert.True(StatutorySubscriptionGuard.CanDisable(isStatutory: false, hasPrincipalApproval: false));
        }

        [Fact]
        [BusinessRule("BR-NTF-002")]
        public void Statutory_rules_need_principal_approval()
        {
            Assert.False(StatutorySubscriptionGuard.CanDisable(isStatutory: true, hasPrincipalApproval: false));
            Assert.True(StatutorySubscriptionGuard.CanDisable(isStatutory: true, hasPrincipalApproval: true));
        }
    }
}
