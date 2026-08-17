using Sms.Application.Activities;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Activities
{
    public class ProgramCapacityEvaluatorTests
    {
        [Theory]
        [InlineData(0, 20, true)]
        [InlineData(19, 20, true)]
        [InlineData(20, 20, false)]
        [InlineData(21, 20, false)]
        [BusinessRule("BR-ACT-002")]
        public void HasCapacity_is_false_once_full(int currentActive, int capacity, bool expected)
        {
            Assert.Equal(expected, ProgramCapacityEvaluator.HasCapacity(currentActive, capacity));
        }
    }
}
