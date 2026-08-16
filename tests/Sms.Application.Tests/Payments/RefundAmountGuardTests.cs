using Sms.Application.Payments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Payments
{
    public class RefundAmountGuardTests
    {
        [Theory]
        [InlineData(100, 100, true)]
        [InlineData(50, 100, true)]
        [InlineData(100.01, 100, false)]
        [InlineData(0, 100, false)]
        [InlineData(-10, 100, false)]
        [BusinessRule("BR-PAY-005")]
        public void Refund_never_exceeds_the_refundable_position(decimal refundAmount, decimal refundablePosition, bool expected)
        {
            Assert.Equal(expected, RefundAmountGuard.IsWithinRefundablePosition(refundAmount, refundablePosition));
        }
    }
}
