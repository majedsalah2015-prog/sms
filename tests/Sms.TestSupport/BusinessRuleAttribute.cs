using System;
using Xunit.Sdk;

namespace Sms.TestSupport
{
    /// <summary>
    /// Marks a test as verifying a numbered business rule from the Analysis v1.0
    /// baseline (e.g. "BR-FEE-012"). The CI BR-coverage gate (NF-M5) diffs these
    /// traits against the rule ids extracted from docs/ — every numbered BR must
    /// map to at least one tagged test before its epic can close.
    /// </summary>
    [TraitDiscoverer("Sms.TestSupport.BusinessRuleDiscoverer", "Sms.TestSupport")]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public sealed class BusinessRuleAttribute : Attribute, ITraitAttribute
    {
        public BusinessRuleAttribute(string ruleId)
        {
            RuleId = ruleId;
        }

        public string RuleId { get; }
    }
}
