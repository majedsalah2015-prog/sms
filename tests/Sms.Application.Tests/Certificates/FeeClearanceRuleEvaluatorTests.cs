using Sms.Application.Certificates;
using Sms.Domain.Certificates;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Certificates
{
    public class FeeClearanceRuleEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-CRT-008")]
        public void Disabled_is_always_clear()
        {
            Assert.True(FeeClearanceRuleEvaluator.IsClear(FeeClearanceRule.Disabled, position: 5000m, overduePosition: 5000m));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(-10, true)]
        [InlineData(0.01, false)]
        [BusinessRule("BR-CRT-008")]
        public void FullClearance_requires_a_non_positive_position(decimal position, bool expected)
        {
            Assert.Equal(expected, FeeClearanceRuleEvaluator.IsClear(FeeClearanceRule.FullClearance, position, overduePosition: 0m));
        }

        [Fact]
        [BusinessRule("BR-CRT-008")]
        public void NoOverdue_only_looks_at_the_overdue_slice()
        {
            Assert.True(FeeClearanceRuleEvaluator.IsClear(FeeClearanceRule.NoOverdue, position: 1000m, overduePosition: 0m));
            Assert.False(FeeClearanceRuleEvaluator.IsClear(FeeClearanceRule.NoOverdue, position: 1000m, overduePosition: 200m));
        }
    }
}
