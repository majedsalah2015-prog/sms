using System;
using System.Security.Cryptography;

namespace Sms.Application.Security
{
    /// <summary>
    /// The password an administrator hands over when an account is provisioned or reset
    /// (BR-SEC-005, doc 06 §3). It is generated here rather than chosen by the administrator for two
    /// reasons: a password a person invents for somebody else is usually the same one they invented
    /// for the last three people, and a password nobody typed cannot be "the one we always use".
    /// <para>
    /// The alphabet is deliberately narrower than the policy requires. This value is read aloud over
    /// a telephone, copied off a printed slip, or typed from a note on a desk, so the characters that
    /// have to be spelled out — <c>0</c> against <c>O</c>, <c>1</c> against <c>l</c> against
    /// <c>I</c> — are simply absent. A one-time password that cannot be transcribed is a support call
    /// on the day it is issued, and the entropy lost to dropping six characters is bought back many
    /// times over by the length.
    /// </para>
    /// <para>
    /// Every result satisfies <see cref="PasswordPolicy.ProductMinimum"/> by construction: one
    /// character of each required class is placed first, the rest is drawn from the whole alphabet,
    /// and the lot is shuffled so the classes do not land in a predictable order.
    /// </para>
    /// </summary>
    public static class OneTimePassword
    {
        /// <summary>Comfortably past <see cref="PasswordPolicy.MinLength"/>, and still short enough to read out.</summary>
        public const int Length = 12;

        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";

        private const string Lower = "abcdefghijkmnopqrstuvwxyz";

        private const string Digits = "23456789";

        /// <summary>Symbols that survive a keyboard layout change and a telephone. No quotes, no backslash, no space.</summary>
        private const string Symbols = "!#$%*+-=?@";

        private const string All = Upper + Lower + Digits + Symbols;

        public static string Generate(int length = Length)
        {
            if (length < PasswordPolicy.ProductMinimum.MinLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length), length,
                    $"A one-time password is at least {PasswordPolicy.ProductMinimum.MinLength} characters (BR-SEC-001).");
            }

            var chars = new char[length];
            chars[0] = Pick(Upper);
            chars[1] = Pick(Lower);
            chars[2] = Pick(Digits);
            chars[3] = Pick(Symbols);

            for (var i = 4; i < length; i++)
            {
                chars[i] = Pick(All);
            }

            Shuffle(chars);
            return new string(chars);
        }

        private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        private static void Shuffle(char[] chars)
        {
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
        }
    }
}
