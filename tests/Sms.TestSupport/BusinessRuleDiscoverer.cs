using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Sms.TestSupport
{
    public sealed class BusinessRuleDiscoverer : ITraitDiscoverer
    {
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            var ruleId = traitAttribute.GetConstructorArguments().FirstOrDefault()?.ToString() ?? string.Empty;
            yield return new KeyValuePair<string, string>("BusinessRule", ruleId);
        }
    }
}
