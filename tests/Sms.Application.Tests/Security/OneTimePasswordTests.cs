using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Security
{
    /// <summary>
    /// The password an administrator hands over when an account is provisioned (BR-SEC-005). Two
    /// things are asserted rather than assumed: that it satisfies the policy the very next screen
    /// will enforce, and that it contains no character somebody has to spell out over a telephone.
    /// </summary>
    public class OneTimePasswordTests
    {
        private static readonly char[] Ambiguous = { '0', 'O', '1', 'l', 'I' };

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public void Every_generated_password_satisfies_the_product_minimum()
        {
            foreach (var _ in Enumerable.Range(0, 200))
            {
                var password = OneTimePassword.Generate();
                var violations = PasswordPolicyEvaluator.Validate(password, PasswordPolicy.ProductMinimum);

                Assert.True(
                    violations.Count == 0,
                    $"'{password}' violates {string.Join(", ", violations)} — the value an administrator " +
                    "reads out would be refused by the change-password screen it is used at.");
            }
        }

        [Fact]
        [BusinessRule("BR-SEC-005")]
        public void The_alphabet_excludes_characters_that_cannot_be_read_aloud()
        {
            var produced = new HashSet<char>();
            foreach (var _ in Enumerable.Range(0, 400))
            {
                foreach (var c in OneTimePassword.Generate())
                {
                    produced.Add(c);
                }
            }

            Assert.Empty(produced.Intersect(Ambiguous));
            Assert.DoesNotContain(' ', produced);
        }

        [Fact]
        [BusinessRule("BR-SEC-005")]
        public void Two_accounts_provisioned_in_the_same_minute_do_not_share_a_password()
        {
            var passwords = Enumerable.Range(0, 100).Select(_ => OneTimePassword.Generate()).ToList();

            Assert.Equal(passwords.Count, passwords.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void The_default_length_is_at_least_the_policy_minimum()
        {
            Assert.True(OneTimePassword.Length >= PasswordPolicy.ProductMinimum.MinLength);
            Assert.Equal(OneTimePassword.Length, OneTimePassword.Generate().Length);
        }

        [Fact]
        public void A_length_below_the_policy_minimum_is_refused_rather_than_quietly_produced()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OneTimePassword.Generate(4));
        }
    }
}
