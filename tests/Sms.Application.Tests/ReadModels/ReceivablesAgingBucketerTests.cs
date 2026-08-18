using System;
using Sms.Application.ReadModels;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.ReadModels
{
    public class ReceivablesAgingBucketerTests
    {
        private static readonly DateTime AsOf = new(2027, 6, 30);

        [Theory]
        [BusinessRule("BR-FEE-008")]
        [InlineData(0, AgingBucket.Current)]
        [InlineData(30, AgingBucket.Current)]     // inside the 30-day current window
        [InlineData(31, AgingBucket.Days1To30)]
        [InlineData(60, AgingBucket.Days1To30)]
        [InlineData(61, AgingBucket.Days31To60)]
        [InlineData(90, AgingBucket.Days31To60)]
        [InlineData(91, AgingBucket.Days61To90)]
        [InlineData(120, AgingBucket.Days61To90)]
        [InlineData(121, AgingBucket.Over90)]
        [InlineData(400, AgingBucket.Over90)]
        public void Buckets_by_days_past_the_current_window(int daysOld, AgingBucket expected)
        {
            Assert.Equal(expected, ReceivablesAgingBucketer.Bucket(AsOf.AddDays(-daysOld), AsOf));
        }

        [Fact]
        public void Current_window_is_configurable()
        {
            Assert.Equal(AgingBucket.Days1To30, ReceivablesAgingBucketer.Bucket(AsOf.AddDays(-10), AsOf, currentDays: 0));
            Assert.Equal(AgingBucket.Current, ReceivablesAgingBucketer.Bucket(AsOf.AddDays(-59), AsOf, currentDays: 60));
        }

        [Fact]
        public void Future_dated_references_are_current()
        {
            Assert.Equal(AgingBucket.Current, ReceivablesAgingBucketer.Bucket(AsOf.AddDays(5), AsOf));
        }
    }
}
