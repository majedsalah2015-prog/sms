using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Setup
{
    /// <summary>
    /// Pure BR-SET-006 dependency rules over a snapshot of effective states
    /// (feature code → enabled). Enabling a feature requires every
    /// dependency on; disabling one is blocked while any dependent is still
    /// on — the caller (screen) shows the offending codes as the "dependency
    /// warning" and lets the operator switch those first.
    /// </summary>
    public static class FeatureDependencyEvaluator
    {
        /// <summary>Codes blocking the requested change; empty = allowed.</summary>
        public static IReadOnlyList<string> Blockers(string code, bool enable, IReadOnlyDictionary<string, bool> effective)
        {
            if (!FeatureCatalog.TryGet(code, out var feature))
            {
                throw new ArgumentException($"Unknown feature '{code}'.", nameof(code));
            }

            if (enable)
            {
                return feature.DependsOn.Where(d => !IsOn(d, effective)).ToList();
            }

            return FeatureCatalog.Dependents(code).Where(f => IsOn(f.Code, effective)).Select(f => f.Code).ToList();
        }

        /// <summary>Effective state = explicit toggle row if present, else the catalog default.</summary>
        public static bool IsOn(string code, IReadOnlyDictionary<string, bool> effective) =>
            effective.TryGetValue(code, out var on) ? on : FeatureCatalog.TryGet(code, out var f) && f.DefaultEnabled;
    }
}
