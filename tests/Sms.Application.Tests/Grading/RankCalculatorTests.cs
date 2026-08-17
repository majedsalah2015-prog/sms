using System.Linq;
using Sms.Application.Grading;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Grading
{
    public class RankCalculatorTests
    {
        [Fact]
        [BusinessRule("BR-GRA-007")]
        public void Distinct_scores_rank_in_descending_order()
        {
            var ranked = RankCalculator.Rank(new (int, decimal)[] { (1, 70m), (2, 90m), (3, 80m) });

            Assert.Equal(1, ranked.Single(r => r.Id == 2).Rank);
            Assert.Equal(2, ranked.Single(r => r.Id == 3).Rank);
            Assert.Equal(3, ranked.Single(r => r.Id == 1).Rank);
        }

        [Fact]
        [BusinessRule("BR-GRA-007")]
        public void Tied_scores_share_rank_and_the_next_rank_skips()
        {
            var ranked = RankCalculator.Rank(new (int, decimal)[] { (1, 90m), (2, 90m), (3, 80m) });

            Assert.Equal(1, ranked.Single(r => r.Id == 1).Rank);
            Assert.Equal(1, ranked.Single(r => r.Id == 2).Rank);
            Assert.Equal(3, ranked.Single(r => r.Id == 3).Rank);
        }
    }
}
