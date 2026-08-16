using Sms.Application.Payments;
using Sms.Domain.Payments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Payments
{
    public class PdcStatusTransitionsTests
    {
        [Theory]
        [InlineData(PdcStatus.Lodged, PdcStatus.Due)]
        [InlineData(PdcStatus.Due, PdcStatus.Deposited)]
        [InlineData(PdcStatus.Deposited, PdcStatus.Cleared)]
        [InlineData(PdcStatus.Deposited, PdcStatus.Bounced)]
        [InlineData(PdcStatus.Bounced, PdcStatus.Replaced)]
        [InlineData(PdcStatus.Bounced, PdcStatus.Settled)]
        [BusinessRule("BR-PAY-004")]
        public void Legal_moves_are_allowed(PdcStatus from, PdcStatus to)
        {
            Assert.True(PdcStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(PdcStatus.Lodged, PdcStatus.Cleared)]
        [InlineData(PdcStatus.Cleared, PdcStatus.Bounced)]
        [InlineData(PdcStatus.Settled, PdcStatus.Lodged)]
        [BusinessRule("BR-PAY-004")]
        public void Illegal_moves_are_rejected(PdcStatus from, PdcStatus to)
        {
            Assert.False(PdcStatusTransitions.CanTransition(from, to));
        }
    }
}
