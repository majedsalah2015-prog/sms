using Sms.Application.Payments;
using Sms.Domain.Payments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Payments
{
    public class RefundVoucherStatusTransitionsTests
    {
        [Theory]
        [InlineData(RefundVoucherStatus.Requested, RefundVoucherStatus.Approved)]
        [InlineData(RefundVoucherStatus.Requested, RefundVoucherStatus.Rejected)]
        [InlineData(RefundVoucherStatus.Approved, RefundVoucherStatus.Paid)]
        [InlineData(RefundVoucherStatus.Approved, RefundVoucherStatus.Rejected)]
        [BusinessRule("BR-PAY-005")]
        public void Legal_moves_are_allowed(RefundVoucherStatus from, RefundVoucherStatus to)
        {
            Assert.True(RefundVoucherStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(RefundVoucherStatus.Requested, RefundVoucherStatus.Paid)]
        [InlineData(RefundVoucherStatus.Paid, RefundVoucherStatus.Approved)]
        [InlineData(RefundVoucherStatus.Rejected, RefundVoucherStatus.Approved)]
        [BusinessRule("BR-PAY-005")]
        public void Illegal_moves_are_rejected(RefundVoucherStatus from, RefundVoucherStatus to)
        {
            Assert.False(RefundVoucherStatusTransitions.CanTransition(from, to));
        }
    }
}
