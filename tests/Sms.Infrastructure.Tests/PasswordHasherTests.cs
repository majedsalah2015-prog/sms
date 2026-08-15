using Sms.Infrastructure.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    public class PasswordHasherTests
    {
        private readonly PasswordHasher _hasher = new();

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public void The_stored_hash_never_equals_the_plaintext()
        {
            var hash = _hasher.Hash("Correct1!Pass");

            Assert.NotEqual("Correct1!Pass", hash);
        }

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public void A_matching_password_verifies()
        {
            var hash = _hasher.Hash("Correct1!Pass");

            Assert.True(_hasher.Verify(hash, "Correct1!Pass"));
        }

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public void A_non_matching_password_fails_verification()
        {
            var hash = _hasher.Hash("Correct1!Pass");

            Assert.False(_hasher.Verify(hash, "SomethingElse1!"));
        }

        [Fact]
        [BusinessRule("BR-SEC-001")]
        public void Hashing_the_same_password_twice_yields_different_salted_hashes()
        {
            var first = _hasher.Hash("Correct1!Pass");
            var second = _hasher.Hash("Correct1!Pass");

            Assert.NotEqual(first, second);
        }
    }
}
