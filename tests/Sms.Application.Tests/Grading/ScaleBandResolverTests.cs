using System.Collections.Generic;
using Sms.Application.Grading;
using Sms.TestSupport;
using Xunit;
using Band = Sms.Application.Grading.ScaleBandResolver.Band;

namespace Sms.Application.Tests.Grading
{
    public class ScaleBandResolverTests
    {
        private static readonly List<Band> Bands = new()
        {
            new Band(1, 90m, 100m), // A
            new Band(2, 80m, 89.99m), // B
            new Band(3, 70m, 79.99m), // C
            new Band(4, 0m, 69.99m), // F
        };

        [Theory]
        [InlineData(95, 1)]
        [InlineData(90, 1)]
        [InlineData(85, 2)]
        [InlineData(75, 3)]
        [InlineData(50, 4)]
        [InlineData(0, 4)]
        [BusinessRule("BR-GRA-001")]
        public void Resolve_finds_the_band_containing_the_score(decimal score, int expectedBandId)
        {
            Assert.Equal(expectedBandId, ScaleBandResolver.Resolve(score, Bands));
        }

        [Fact]
        [BusinessRule("BR-GRA-001")]
        public void Resolve_returns_null_when_no_band_covers_the_score()
        {
            var sparseBands = new List<Band> { new(1, 50m, 100m) };

            Assert.Null(ScaleBandResolver.Resolve(20m, sparseBands));
        }

        [Fact]
        [BusinessRule("BR-GRA-001")]
        public void Non_overlapping_bands_pass_validation()
        {
            Assert.True(ScaleBandResolver.AreNonOverlapping(Bands));
        }

        [Fact]
        [BusinessRule("BR-GRA-001")]
        public void Overlapping_bands_fail_validation()
        {
            var overlapping = new List<Band> { new(1, 80m, 100m), new(2, 90m, 100m) };

            Assert.False(ScaleBandResolver.AreNonOverlapping(overlapping));
        }
    }
}
