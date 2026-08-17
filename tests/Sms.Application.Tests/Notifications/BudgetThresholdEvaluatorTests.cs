using Sms.Application.Notifications;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Notifications
{
    public class BudgetThresholdEvaluatorTests
    {
        [Theory]
        [InlineData(79, 100, false)]
        [InlineData(80, 100, true)]
        [InlineData(100, 100, true)]
        [BusinessRule("BR-NTF-004")]
        public void ShouldAlert_at_80_percent(int count, int limit, bool expected)
        {
            Assert.Equal(expected, BudgetThresholdEvaluator.ShouldAlert(count, limit));
        }

        [Theory]
        [InlineData(99, 100, false, false)]
        [InlineData(100, 100, false, true)]
        [InlineData(100, 100, true, false)]
        [BusinessRule("BR-NTF-004")]
        public void ShouldBlock_at_100_percent_unless_safety_class(int count, int limit, bool isSafetyClass, bool expected)
        {
            Assert.Equal(expected, BudgetThresholdEvaluator.ShouldBlock(count, limit, isSafetyClass));
        }
    }
}
