using Sms.Application.Dashboards;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Dashboards
{
    public class AnonymityThresholdGuardTests
    {
        [Theory]
        [InlineData(0, 5, false)]
        [InlineData(4, 5, true)]
        [InlineData(5, 5, false)]
        [InlineData(6, 5, false)]
        [BusinessRule("BR-DSH-007")]
        public void ShouldMask_only_small_nonzero_counts(int count, int threshold, bool expected)
        {
            Assert.Equal(expected, AnonymityThresholdGuard.ShouldMask(count, threshold));
        }

        [Fact]
        [BusinessRule("BR-DSH-007")]
        public void Mask_returns_null_when_masked_else_the_real_count()
        {
            Assert.Null(AnonymityThresholdGuard.Mask(3, 5));
            Assert.Equal(7, AnonymityThresholdGuard.Mask(7, 5));
            Assert.Equal(0, AnonymityThresholdGuard.Mask(0, 5));
        }
    }
}
