using Sms.Application.Audit;
using Sms.Domain.Audit;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Audit
{
    public class AnomalyDispositionPolicyTests
    {
        [Fact]
        [BusinessRule("BR-AUM-002")]
        public void Only_an_open_hit_can_be_dispositioned()
        {
            Assert.True(AnomalyDispositionPolicy.CanDispose(AnomalyHitStatus.Open));
            Assert.False(AnomalyDispositionPolicy.CanDispose(AnomalyHitStatus.Dismissed));
            Assert.False(AnomalyDispositionPolicy.CanDispose(AnomalyHitStatus.Escalated));
        }
    }
}
