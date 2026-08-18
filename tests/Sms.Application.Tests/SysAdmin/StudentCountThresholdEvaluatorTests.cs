using Sms.Application.SysAdmin;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.SysAdmin
{
    public class StudentCountThresholdEvaluatorTests
    {
        [Theory]
        [InlineData(89, 100, false)]
        [InlineData(90, 100, true)]
        [InlineData(100, 100, true)]
        [BusinessRule("BR-SYS-006")]
        public void Approaching_limit_at_90_percent(int count, int cap, bool expected)
        {
            Assert.Equal(expected, StudentCountThresholdEvaluator.IsApproachingLimit(count, cap));
        }

        [Theory]
        [InlineData(100, 100, false)]
        [InlineData(101, 100, true)]
        [BusinessRule("BR-SYS-006")]
        public void Over_cap_only_strictly_above(int count, int cap, bool expected)
        {
            Assert.Equal(expected, StudentCountThresholdEvaluator.IsOverCap(count, cap));
        }
    }
}
