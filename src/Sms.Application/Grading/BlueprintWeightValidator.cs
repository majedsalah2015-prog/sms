using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Grading
{
    /// <summary>Pure BR-GRA-003 validation rule: a Blueprint's component weights must sum to exactly 100 before it can be finalized.</summary>
    public static class BlueprintWeightValidator
    {
        public static bool SumsTo100(IEnumerable<decimal> componentWeights)
            => componentWeights.Sum() == 100m;
    }
}
