using Sms.Application.Fees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Fees
{
    public class StudentFinancialPositionCalculatorTests
    {
        [Theory]
        [InlineData(1000, 0, 0, 1000)]
        [InlineData(1000, 100, 0, 900)]
        [InlineData(1000, 100, 400, 500)]
        [InlineData(1000, 0, 1000, 0)]
        [BusinessRule("BR-FEE-008")]
        public void Position_is_charges_minus_credit_notes_minus_allocated_payments(
            decimal charges, decimal creditNotes, decimal allocated, decimal expected)
        {
            Assert.Equal(expected, StudentFinancialPositionCalculator.Calculate(charges, creditNotes, allocated));
        }
    }
}
