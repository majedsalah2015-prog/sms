using System;

namespace Sms.Application.Localization
{
    /// <summary>
    /// doc 02 §6: "Arabic-Indic digit display is a UI preference, storage is
    /// invariant." Pure presentation transform — never applied to a stored
    /// value (matches BR-NUM-007's identical rule for numbering series).
    /// </summary>
    public static class ArabicIndicDigitFormatter
    {
        private static readonly char[] ArabicIndicDigits = { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };

        public static string ToArabicIndicDigits(string input)
        {
            var chars = input.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '0' && chars[i] <= '9')
                {
                    chars[i] = ArabicIndicDigits[chars[i] - '0'];
                }
            }

            return new string(chars);
        }

        public static string ToWesternDigits(string input)
        {
            var chars = input.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var index = Array.IndexOf(ArabicIndicDigits, chars[i]);
                if (index >= 0)
                {
                    chars[i] = (char)('0' + index);
                }
            }

            return new string(chars);
        }
    }
}
