using Sms.Application.Activities;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Activities
{
    public class ConsentGateTests
    {
        [Fact]
        [BusinessRule("BR-ACT-005")]
        public void No_consent_required_always_allows()
        {
            Assert.True(ConsentGate.CanParticipate(requiresConsent: false, hasConsentRecord: false));
        }

        [Fact]
        [BusinessRule("BR-ACT-005")]
        public void Required_without_a_record_is_blocked_hard()
        {
            Assert.False(ConsentGate.CanParticipate(requiresConsent: true, hasConsentRecord: false));
        }

        [Fact]
        [BusinessRule("BR-ACT-005")]
        public void Required_with_a_record_is_allowed()
        {
            Assert.True(ConsentGate.CanParticipate(requiresConsent: true, hasConsentRecord: true));
        }
    }
}
