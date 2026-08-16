using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Grading
{
    /// <summary>
    /// Pure BR-GRA-003 weighted term calculation. Exempted components are
    /// excluded and the remaining weights re-normalized (denominator
    /// reduction — doc's own proposed default for BR-GRA-004's open
    /// question #1; weight redistribution to non-exempt components is the
    /// same arithmetic outcome as denominator reduction here, so no
    /// separate mode is needed). Absent components count as zero (BR-ATD
    /// convention: unexcused absence is not the same as exemption).
    /// </summary>
    public static class TermScoreCalculator
    {
        public readonly struct ComponentMark
        {
            public ComponentMark(decimal? score, decimal maxScore, decimal weight, bool isAbsent, bool isExempt)
            {
                Score = score;
                MaxScore = maxScore;
                Weight = weight;
                IsAbsent = isAbsent;
                IsExempt = isExempt;
            }

            public decimal? Score { get; }

            public decimal MaxScore { get; }

            public decimal Weight { get; }

            public bool IsAbsent { get; }

            public bool IsExempt { get; }
        }

        public static decimal CalculateWeightedPercent(IEnumerable<ComponentMark> components)
        {
            var included = components.Where(c => !c.IsExempt).ToList();
            var totalWeight = included.Sum(c => c.Weight);
            if (totalWeight <= 0)
            {
                return 0m;
            }

            var weightedSum = 0m;
            foreach (var component in included)
            {
                var achievedPercent = component.IsAbsent || component.MaxScore <= 0
                    ? 0m
                    : (component.Score ?? 0m) / component.MaxScore * 100m;
                weightedSum += achievedPercent * component.Weight;
            }

            return weightedSum / totalWeight;
        }

        /// <summary>BR-GRA-003: 2dp, round-half-up at the final band only — no cascading rounds through intermediate calculations.</summary>
        public static decimal RoundHalfUp(decimal value, int decimals = 2)
            => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
