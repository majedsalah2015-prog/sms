using System;
using System.Security.Cryptography;
using System.Text;

namespace Sms.Application.Security
{
    /// <summary>
    /// BR-SEC-003 TOTP (RFC 6238 over RFC 4226 HOTP, HMAC-SHA1/30s/6-digit —
    /// the parameters every authenticator app assumes). Self-contained on the
    /// BCL: no SMS/email dispatch, so no dependency on E-007.
    /// </summary>
    public static class TwoFactorTotp
    {
        private const int StepSeconds = 30;
        private const int Digits = 6;
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>160-bit shared secret (RFC 4226 §4 recommendation), Base32 for QR/manual entry.</summary>
        public static string GenerateSecretKey()
        {
            var bytes = new byte[20];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base32Encode(bytes);
        }

        public static string ComputeCode(string secretKeyBase32, DateTime utcNow)
        {
            var counter = CounterFor(utcNow);
            return Hotp(secretKeyBase32, counter);
        }

        /// <summary>Accepts the current step and one step of clock drift either side (BR-SEC-003).</summary>
        public static bool ValidateCode(string secretKeyBase32, string code, DateTime utcNow, int windowSteps = 1)
        {
            var counter = CounterFor(utcNow);
            for (var offset = -windowSteps; offset <= windowSteps; offset++)
            {
                if (Hotp(secretKeyBase32, counter + offset) == code)
                {
                    return true;
                }
            }

            return false;
        }

        private static long CounterFor(DateTime utcNow)
            => new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToUnixTimeSeconds() / StepSeconds;

        private static string Hotp(string secretKeyBase32, long counter)
        {
            var key = Base32Decode(secretKeyBase32);
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(counterBytes);
            }

            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(counterBytes);

            var offset = hash[^1] & 0x0F;
            var binary = ((hash[offset] & 0x7F) << 24)
                         | ((hash[offset + 1] & 0xFF) << 16)
                         | ((hash[offset + 2] & 0xFF) << 8)
                         | (hash[offset + 3] & 0xFF);

            var truncated = binary % (int)Math.Pow(10, Digits);
            return truncated.ToString(new string('0', Digits));
        }

        private static string Base32Encode(byte[] data)
        {
            var result = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0, bitsLeft = 0;

            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    result.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
                }
            }

            if (bitsLeft > 0)
            {
                result.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
            }

            return result.ToString();
        }

        private static byte[] Base32Decode(string base32)
        {
            var bytes = new byte[base32.Length * 5 / 8];
            int buffer = 0, bitsLeft = 0, index = 0;

            foreach (var c in base32)
            {
                var value = Base32Alphabet.IndexOf(char.ToUpperInvariant(c));
                if (value < 0)
                {
                    continue;
                }

                buffer = (buffer << 5) | value;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    bytes[index++] = (byte)((buffer >> bitsLeft) & 0xFF);
                }
            }

            return bytes;
        }
    }
}
