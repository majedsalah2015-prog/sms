using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Reports
{
    /// <summary>Pure doc/Modules/30 §9: mandatory parameters enforced before a report runs.</summary>
    public static class RequiredParameterEvaluator
    {
        public static IReadOnlyList<string> ParseRequiredKeys(string? requiredKeysCsv)
            => string.IsNullOrWhiteSpace(requiredKeysCsv)
                ? Array.Empty<string>()
                : requiredKeysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public static IReadOnlyList<string> FindMissing(IReadOnlyList<string> requiredKeys, IEnumerable<string> suppliedKeys)
        {
            var supplied = new HashSet<string>(suppliedKeys, StringComparer.OrdinalIgnoreCase);
            return requiredKeys.Where(k => !supplied.Contains(k)).ToList();
        }
    }
}
