using Sms.Application.Reports;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Reports
{
    public class HeavyReportQueueEvaluatorTests
    {
        [Theory]
        [InlineData(4999, 5000, false)]
        [InlineData(5000, 5000, true)]
        [InlineData(5001, 5000, true)]
        [BusinessRule("BR-RPT-005")]
        public void Queues_at_or_above_the_threshold(int estimatedRowCount, int threshold, bool expected)
        {
            Assert.Equal(expected, HeavyReportQueueEvaluator.ShouldQueue(estimatedRowCount, threshold));
        }
    }
}
