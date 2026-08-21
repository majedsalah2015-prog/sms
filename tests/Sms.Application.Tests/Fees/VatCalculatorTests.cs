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

        [Fact]
        [BusinessRule("BR-GLB-061")]
        public void A_gross_figure_splits_back_into_the_net_and_vat_that_made_it()
        {
            var (vat, net) = VatCalculator.CalculateFromGross(1150m, 0.15m);

            Assert.Equal(150m, vat);
            Assert.Equal(1000m, net);
        }

        [Fact]
        [BusinessRule("BR-GLB-060")]
        public void Splitting_a_gross_figure_never_loses_or_invents_a_cent()
        {
            // Every gross from 0.01 to 200.00 at 15%: the halves must add back to exactly what went in.
            // Dividing by 1.15 and re-grossing fails this — which is how a claw-back ends up a cent off
            // the discount it reverses.
            for (var cents = 1; cents <= 20000; cents++)
            {
                var gross = cents / 100m;
                var (vat, net) = VatCalculator.CalculateFromGross(gross, 0.15m);
                Assert.Equal(gross, net + vat);
            }
        }

        [Fact]
        [BusinessRule("BR-GLB-061")]
        public void An_exempt_gross_figure_is_all_net()
        {
            Assert.Equal((0m, 500m), VatCalculator.CalculateFromGross(500m, null));
            Assert.Equal((0m, 500m), VatCalculator.CalculateFromGross(500m, 0m));
        }
    }
}
