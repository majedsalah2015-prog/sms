using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Security
{
    /// <summary>
    /// Pure BR-SEC-001 shape checks. Reuse-against-history is a separate
    /// concern (needs the hasher to compare against stored hashes) — callers
    /// append <see cref="PasswordPolicyViolation.ReusesRecentPassword"/>
    /// themselves; see <see cref="IAuthenticationService"/>.
    /// </summary>
    public static class PasswordPolicyEvaluator
    {
        public static IReadOnlyList<PasswordPolicyViolation> Validate(string candidatePassword, PasswordPolicy policy)
        {
            var violations = new List<PasswordPolicyViolation>();

            if (candidatePassword.Length < policy.MinLength)
            {
                violations.Add(PasswordPolicyViolation.TooShort);
            }

            if (policy.RequireUpper && !candidatePassword.Any(char.IsUpper))
            {
                violations.Add(PasswordPolicyViolation.MissingUppercase);
            }

            if (policy.RequireLower && !candidatePassword.Any(char.IsLower))
            {
                violations.Add(PasswordPolicyViolation.MissingLowercase);
            }

            if (policy.RequireDigit && !candidatePassword.Any(char.IsDigit))
            {
                violations.Add(PasswordPolicyViolation.MissingDigit);
            }

            if (policy.RequireSymbol && candidatePassword.All(c => char.IsLetterOrDigit(c)))
            {
                violations.Add(PasswordPolicyViolation.MissingSymbol);
            }

            return violations;
        }
    }
}
