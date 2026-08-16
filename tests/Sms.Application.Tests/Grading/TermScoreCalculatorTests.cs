using Sms.Application.Grading;
using Sms.TestSupport;
using Xunit;
using ComponentMark = Sms.Application.Grading.TermScoreCalculator.ComponentMark;

namespace Sms.Application.Tests.Grading
{
    public class TermScoreCalculatorTests
    {
        [Fact]
        [BusinessRule("BR-GRA-003")]
        public void Weighted_percent_combines_components_by_weight()
        {
            var marks = new[]
            {
                new ComponentMark(score: 80m, maxScore: 100m, weight: 50m, isAbsent: false, isExempt: false),
                new ComponentMark(score: 60m, maxScore: 100m, weight: 50m, isAbsent: false, isExempt: false),
            };

            Assert.Equal(70m, TermScoreCalculator.CalculateWeightedPercent(marks));
        }

        [Fact]
        [BusinessRule("BR-GRA-004")]
        public void Exempt_components_are_excluded_and_the_denominator_is_reduced()
        {
            var marks = new[]
            {
                new ComponentMark(score: 90m, maxScore: 100m, weight: 30m, isAbsent: false, isExempt: false),
                new ComponentMark(score: 70m, maxScore: 100m, weight: 30m, isAbsent: false, isExempt: false),
                new ComponentMark(score: null, maxScore: 100m, weight: 40m, isAbsent: false, isExempt: true),
            };

            // only the two non-exempt components count, re-weighted over their own 60-point total
            Assert.Equal(80m, TermScoreCalculator.CalculateWeightedPercent(marks));
        }

        [Fact]
        [BusinessRule("BR-ATD-002")]
        public void Absent_components_count_as_zero_not_excluded()
        {
            var marks = new[]
            {
                new ComponentMark(score: 100m, maxScore: 100m, weight: 50m, isAbsent: false, isExempt: false),
                new ComponentMark(score: null, maxScore: 100m, weight: 50m, isAbsent: true, isExempt: false),
            };

            Assert.Equal(50m, TermScoreCalculator.CalculateWeightedPercent(marks));
        }

        [Fact]
        [BusinessRule("BR-GRA-003")]
        public void All_components_exempt_yields_zero_not_a_division_error()
        {
            var marks = new[]
            {
                new ComponentMark(score: null, maxScore: 100m, weight: 100m, isAbsent: false, isExempt: true),
            };

            Assert.Equal(0m, TermScoreCalculator.CalculateWeightedPercent(marks));
        }

        [Theory]
        [InlineData(66.665, 66.67)]
        [InlineData(66.664, 66.66)]
        [InlineData(50, 50)]
        [BusinessRule("BR-GRA-003")]
        public void RoundHalfUp_rounds_to_two_decimals_away_from_zero_on_the_midpoint(decimal value, decimal expected)
        {
            Assert.Equal(expected, TermScoreCalculator.RoundHalfUp(value));
        }
    }
}
