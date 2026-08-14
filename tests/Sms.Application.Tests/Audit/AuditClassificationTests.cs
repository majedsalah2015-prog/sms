using Sms.Application.Audit;
using Sms.Domain.Audit;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Audit
{
    public class AuditClassificationTests
    {
        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void Configuration_can_raise_a_tier()
        {
            Assert.Equal(AuditTier.T1, AuditClassification.Effective(AuditTier.T2, AuditTier.T1));
            Assert.Equal(AuditTier.T2, AuditClassification.Effective(AuditTier.T3, AuditTier.T2));
        }

        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void Configuration_can_never_lower_a_tier_below_its_assignment()
        {
            Assert.Equal(AuditTier.T1, AuditClassification.Effective(AuditTier.T1, AuditTier.T3));
            Assert.Equal(AuditTier.T2, AuditClassification.Effective(AuditTier.T2, AuditTier.T3));
        }

        [Fact]
        [BusinessRule("BR-AUD-002")]
        public void No_configuration_means_the_assigned_tier()
        {
            Assert.Equal(AuditTier.T2, AuditClassification.Effective(AuditTier.T2, null));
        }
    }
}
