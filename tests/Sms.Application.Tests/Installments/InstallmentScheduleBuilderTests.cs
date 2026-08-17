using System;
using System.Linq;
using Sms.Application.Installments;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Installments
{
    public class InstallmentScheduleBuilderTests
    {
        private static readonly DateTime[] FourDates =
        {
            new(2026, 9, 1), new(2026, 11, 1), new(2027, 1, 1), new(2027, 3, 1),
        };

        [Fact]
        [BusinessRule("BR-INS-002")]
        public void Rounding_difference_is_absorbed_in_the_last_installment()
        {
            var schedule = InstallmentScheduleBuilder.Build(1000m, new[] { 33.33m, 33.33m, 33.34m }, FourDates.Take(3).ToList());

            Assert.Equal(new[] { 333.30m, 333.30m, 333.40m }, schedule.Select(s => s.Amount));
            Assert.Equal(1000m, schedule.Sum(s => s.Amount));
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public void Schedule_sums_exactly_to_the_total_for_an_awkward_amount()
        {
            var schedule = InstallmentScheduleBuilder.Build(1234.57m, new[] { 25m, 25m, 25m, 25m }, FourDates);

            Assert.Equal(1234.57m, schedule.Sum(s => s.Amount));
            Assert.Equal(308.64m, schedule[0].Amount);
            Assert.Equal(308.65m, schedule[3].Amount);
        }

        [Fact]
        [BusinessRule("BR-INS-001")]
        public void Splits_that_do_not_sum_to_hundred_are_rejected()
        {
            Assert.Throws<ArgumentException>(() => InstallmentScheduleBuilder.Build(100m, new[] { 50m, 40m }, FourDates.Take(2).ToList()));
        }

        [Fact]
        [BusinessRule("BR-INS-003")]
        public void SpreadEvenly_puts_the_remainder_in_the_last_slot()
        {
            Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, InstallmentScheduleBuilder.SpreadEvenly(100m, 3));
        }

        [Fact]
        [BusinessRule("BR-INS-002")]
        public void Charges_waterfall_into_installments_in_order()
        {
            var charges = new[]
            {
                new InstallmentScheduleBuilder.ChargePortion(1, 600m), new InstallmentScheduleBuilder.ChargePortion(2, 400m),
            };

            var lines = InstallmentScheduleBuilder.MapChargesToInstallments(charges, new[] { 500m, 500m });

            Assert.Equal(3, lines.Count);
            Assert.Equal((0, 1, 500m), (lines[0].InstallmentIndex, lines[0].ChargeId, lines[0].Amount));
            Assert.Equal((1, 1, 100m), (lines[1].InstallmentIndex, lines[1].ChargeId, lines[1].Amount));
            Assert.Equal((1, 2, 400m), (lines[2].InstallmentIndex, lines[2].ChargeId, lines[2].Amount));
        }
    }
}
