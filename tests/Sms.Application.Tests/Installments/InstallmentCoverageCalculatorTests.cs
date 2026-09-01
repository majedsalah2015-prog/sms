using System.Collections.Generic;
using Sms.Application.Installments;
using Sms.TestSupport;
using Xunit;
using Line = Sms.Application.Installments.InstallmentCoverageCalculator.ScheduleLine;

namespace Sms.Application.Tests.Installments
{
    public class InstallmentCoverageCalculatorTests
    {
        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void A_charges_allocation_fills_its_earliest_installment_first()
        {
            var lines = new List<Line> { new(101, 7, 249.98m, 1), new(102, 7, 249.98m, 2), new(103, 7, 249.98m, 3) };

            var covered = InstallmentCoverageCalculator.Cover(lines, new Dictionary<int, decimal> { [7] = 250m });

            Assert.Equal(249.98m, covered[101]);
            Assert.Equal(0.02m, covered[102]);
            Assert.False(covered.ContainsKey(103));
        }

        [Fact]
        [BusinessRule("BR-INS-007")]
        public void An_installment_spanning_two_charges_sums_what_each_line_took()
        {
            var lines = new List<Line> { new(101, 7, 100m, 1), new(101, 8, 50m, 1), new(102, 8, 50m, 2) };

            var covered = InstallmentCoverageCalculator.Cover(lines, new Dictionary<int, decimal> { [7] = 60m, [8] = 70m });

            // 60 of the first charge, then 50 of the second (its line is satisfied in full).
            Assert.Equal(110m, covered[101]);
            Assert.Equal(20m, covered[102]);
        }

        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void An_unpaid_charge_covers_nothing()
        {
            var lines = new List<Line> { new(101, 7, 100m, 1) };

            var covered = InstallmentCoverageCalculator.Cover(lines, new Dictionary<int, decimal>());

            Assert.Empty(covered);
        }

        [Fact]
        [BusinessRule("BR-PAY-003")]
        public void Overpayment_never_covers_beyond_the_scheduled_lines()
        {
            var lines = new List<Line> { new(101, 7, 100m, 1), new(102, 7, 100m, 2) };

            var covered = InstallmentCoverageCalculator.Cover(lines, new Dictionary<int, decimal> { [7] = 500m });

            Assert.Equal(100m, covered[101]);
            Assert.Equal(100m, covered[102]);
        }
    }
}
