using Sms.Application.Payments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Payments
{
    public class AdvanceBalanceCalculatorTests
    {
        [Theory]
        [InlineData(1000, 1000, 0)]
        [InlineData(1000, 700, 300)]
        [InlineData(0, 0, 0)]
        [BusinessRule("BR-PAY-003")]
        public void Balance_is_receipts_minus_allocated(decimal receipts, decimal allocated, decimal expected)
        {
            Assert.Equal(expected, AdvanceBalanceCalculator.Calculate(receipts, allocated));
        }
    }
}
