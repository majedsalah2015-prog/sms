using System;
using System.Text;
using Sms.Application.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Security
{
    public class TwoFactorTotpTests
    {
        private static readonly DateTime Time = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);

        /// <summary>Minimal local Base32 (RFC 4648) encoder, kept independent of TwoFactorTotp's own implementation on purpose.</summary>
        private static string Base32(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var sb = new StringBuilder();
            int buffer = 0, bitsLeft = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(alphabet[(buffer >> bitsLeft) & 0x1F]);
                }
            }

            if (bitsLeft > 0)
            {
                sb.Append(alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
            }

            return sb.ToString();
        }

        [Fact]
        [BusinessRule("BR-SEC-003")]
        public void Generated_secrets_are_valid_base32()
        {
            var secret = TwoFactorTotp.GenerateSecretKey();

            Assert.NotEmpty(secret);
            Assert.All(secret, c => Assert.Contains(c, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
        }

        [Fact]
        [BusinessRule("BR-SEC-003")]
        public void The_code_for_the_current_step_validates()
        {
            var secret = TwoFactorTotp.GenerateSecretKey();
            var code = TwoFactorTotp.ComputeCode(secret, Time);

            Assert.True(TwoFactorTotp.ValidateCode(secret, code, Time));
        }

        [Fact]
        [BusinessRule("BR-SEC-003")]
        public void A_wrong_code_does_not_validate()
        {
            var secret = TwoFactorTotp.GenerateSecretKey();
            var code = TwoFactorTotp.ComputeCode(secret, Time);
            var wrong = code == "000000" ? "111111" : "000000";

            Assert.False(TwoFactorTotp.ValidateCode(secret, wrong, Time));
        }

        [Fact]
        [BusinessRule("BR-SEC-003")]
        public void One_step_of_clock_drift_is_tolerated_either_side()
        {
            var secret = TwoFactorTotp.GenerateSecretKey();
            var code = TwoFactorTotp.ComputeCode(secret, Time);

            Assert.True(TwoFactorTotp.ValidateCode(secret, code, Time.AddSeconds(30)));
            Assert.True(TwoFactorTotp.ValidateCode(secret, code, Time.AddSeconds(-30)));
        }

        [Fact]
        [BusinessRule("BR-SEC-003")]
        public void A_code_two_steps_away_is_rejected()
        {
            var secret = TwoFactorTotp.GenerateSecretKey();
            var code = TwoFactorTotp.ComputeCode(secret, Time);

            Assert.False(TwoFactorTotp.ValidateCode(secret, code, Time.AddSeconds(61)));
        }

        /// <summary>
        /// RFC 6238 Appendix B reference vectors (HMAC-SHA1, 30s step) truncated
        /// to 6 digits: mod 10^8 → mod 10^6 preserves the low 6 digits since
        /// 10^8 is a multiple of 10^6, so the published 8-digit vectors also
        /// pin the 6-digit output this implementation produces.
        /// </summary>
        [Theory]
        [InlineData(59L, "287082")]
        [InlineData(1111111109L, "081804")]
        [InlineData(1234567890L, "005924")]
        [BusinessRule("BR-SEC-003")]
        public void Matches_the_RFC_6238_reference_vectors(long unixSeconds, string expectedCode)
        {
            var secret = Base32(Encoding.ASCII.GetBytes("12345678901234567890"));
            var time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;

            Assert.Equal(expectedCode, TwoFactorTotp.ComputeCode(secret, time));
        }
    }
}
