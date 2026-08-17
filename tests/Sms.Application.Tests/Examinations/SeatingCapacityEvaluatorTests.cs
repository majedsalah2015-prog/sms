using Sms.Application.Examinations;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Examinations
{
    public class SeatingCapacityEvaluatorTests
    {
        [Theory]
        [InlineData(0, 20, true)]
        [InlineData(19, 20, true)]
        [InlineData(20, 20, false)]
        [InlineData(21, 20, false)]
        [BusinessRule("BR-EXM-004")]
        public void HasCapacity_is_false_once_full(int currentlyAllocated, int examCapacity, bool expected)
        {
            Assert.Equal(expected, SeatingCapacityEvaluator.HasCapacity(currentlyAllocated, examCapacity));
        }
    }
}
