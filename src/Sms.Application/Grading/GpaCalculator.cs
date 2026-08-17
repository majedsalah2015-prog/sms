using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Grading
{
    /// <summary>Pure BR-GRA-007: GPA = weighted average of each offering's scale-band GPA points (weights from BR-SUB-002 offering weights, per doc). Offerings whose band carries no GPA points (e.g. a pass/fail-only scale) are excluded, not treated as zero.</summary>
    public static class GpaCalculator
    {
        public readonly struct OfferingResult
        {
            public OfferingResult(decimal? gpaPoints, decimal weight)
            {
                GpaPoints = gpaPoints;
                Weight = weight;
            }

            public decimal? GpaPoints { get; }

            public decimal Weight { get; }
        }

        public static decimal Calculate(IEnumerable<OfferingResult> results)
        {
            var withPoints = results.Where(r => r.GpaPoints.HasValue).ToList();
            var totalWeight = withPoints.Sum(r => r.Weight);
            if (totalWeight <= 0)
            {
                return 0m;
            }

            var weightedSum = withPoints.Sum(r => r.GpaPoints!.Value * r.Weight);
            return weightedSum / totalWeight;
        }
    }
}
