using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Grading
{
    /// <summary>Pure BR-GRA-007: standard-competition ranking (doc's default tie policy) — tied scores share the same (lower) rank, the next distinct score skips ahead by the tie-group size.</summary>
    public static class RankCalculator
    {
        public readonly struct RankedEntry
        {
            public RankedEntry(int id, int rank)
            {
                Id = id;
                Rank = rank;
            }

            public int Id { get; }

            public int Rank { get; }
        }

        public static IReadOnlyList<RankedEntry> Rank(IEnumerable<(int Id, decimal Score)> scores)
        {
            var ordered = scores.OrderByDescending(s => s.Score).ToList();
            var result = new List<RankedEntry>(ordered.Count);
            var currentRank = 0;
            decimal? previousScore = null;

            for (var i = 0; i < ordered.Count; i++)
            {
                if (previousScore == null || ordered[i].Score != previousScore.Value)
                {
                    currentRank = i + 1;
                }

                result.Add(new RankedEntry(ordered[i].Id, currentRank));
                previousScore = ordered[i].Score;
            }

            return result;
        }
    }
}
