using Sms.Application.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Security
{
    public class PasswordPolicyEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-SEC-001")]
        public void Product_minimum_accepts_a_compliant_password()
        {
            var violations = PasswordPolicyEvaluator.Validate("Str0ng!Pass", PasswordPolicy.ProductMinimum);

            Assert.Empty(violations);
        }

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public void Too_short_is_flagged_even_when_otherwise_complex()
        {
            var violations = PasswordPolicyEvaluator.Validate("Sh0rt!", PasswordPolicy.ProductMinimum);

            Assert.Contains(PasswordPolicyViolation.TooShort, violations);
        }

        [Theory]
        [InlineData("alllowercase1!", PasswordPolicyViolation.MissingUppercase)]
        [InlineData("ALLUPPERCASE1!", PasswordPolicyViolation.MissingLowercase)]
        [InlineData("NoDigitsHere!!", PasswordPolicyViolation.MissingDigit)]
        [InlineData("NoSymbolsHere1", PasswordPolicyViolation.MissingSymbol)]
        [BusinessRule("BR-SEC-001")]
        public void Missing_character_class_is_flagged(string candidate, PasswordPolicyViolation expected)
        {
            var violations = PasswordPolicyEvaluator.Validate(candidate, PasswordPolicy.ProductMinimum);

            Assert.Contains(expected, violations);
        }

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public void A_looser_school_policy_only_checks_what_it_requires()
        {
            var looser = new PasswordPolicy { MinLength = 6, RequireUpper = false, RequireSymbol = false };

            var violations = PasswordPolicyEvaluator.Validate("simple1", looser);

            Assert.Empty(violations);
        }
    }
}
