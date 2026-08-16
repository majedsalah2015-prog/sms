using Sms.Application.Fees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Fees
{
    public class VatCalculatorTests
    {
        [Fact]
        [BusinessRule("BR-GLB-061")]
        public void Null_rate_is_exempt()
        {
            var (vat, gross) = VatCalculator.Calculate(1000m, null);

            Assert.Equal(0m, vat);
            Assert.Equal(1000m, gross);
        }

        [Fact]
        [BusinessRule("BR-GLB-061")]
        public void Fifteen_percent_rate_computes_vat_and_gross()
        {
            var (vat, gross) = VatCalculator.Calculate(1000m, 0.15m);

            Assert.Equal(150m, vat);
            Assert.Equal(1150m, gross);
        }

        [Fact]
        [BusinessRule("BR-GLB-060")]
        public void Vat_rounds_to_two_decimals_away_from_zero()
        {
            var (vat, gross) = VatCalculator.Calculate(33.33m, 0.15m);

            Assert.Equal(5.00m, vat); // 33.33 * 0.15 = 4.9995 -> 5.00
            Assert.Equal(38.33m, gross);
        }
    }
}
