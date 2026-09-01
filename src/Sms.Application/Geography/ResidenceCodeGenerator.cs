using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Geography
{
    /// <summary>
    /// The stable key for a residence row whose author did not supply one.
    /// <para>
    /// <c>Code</c> is what the seeder is idempotent on and what the unique index is built over, so
    /// every row needs one — but the registrar adding a quarter has no view about what it should be
    /// called internally, and made to type one they type "1". Derived from the English name it at
    /// least reads back in an export.
    /// </para>
    /// <para>
    /// Deliberately the same rule the lookup screens use (ASCII, upper case, numeric suffix on a
    /// collision), so a person maintaining two reference lists does not meet two conventions. ASCII
    /// only: the code travels into exports and URLs.
    /// </para>
    /// </summary>
    public static class ResidenceCodeGenerator
    {
        /// <summary>Longest code the schema holds is 20; 16 leaves room for the collision suffix.</summary>
        private const int MaxBaseLength = 16;

        /// <summary>
        /// A code not present in <paramref name="taken"/>, derived from <paramref name="nameEn"/>.
        /// <para>
        /// <paramref name="taken"/> must be the codes of the level's own uniqueness scope — every
        /// governorate of the school, every locality of one governorate, every quarter of one
        /// locality — and must include the deactivated ones, which still own their codes.
        /// </para>
        /// </summary>
        public static string Next(string? nameEn, IEnumerable<string> taken)
        {
            // ASCII by character range, not by char.IsLetterOrDigit: that returns true for Arabic
            // letters, so an operator who pasted the Arabic name into the English box would have
            // produced an Arabic "code" and put it into every export and URL that carries one.
            var baseCode = new string((nameEn ?? string.Empty).ToUpperInvariant()
                .Where(ch => ch is (>= 'A' and <= 'Z') or (>= '0' and <= '9')).ToArray());
            if (baseCode.Length == 0) baseCode = "LOC";
            if (baseCode.Length > MaxBaseLength) baseCode = baseCode.Substring(0, MaxBaseLength);

            var set = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
            if (!set.Contains(baseCode)) return baseCode;

            for (var i = 2; i < 1000; i++)
            {
                var candidate = baseCode + i;
                if (!set.Contains(candidate)) return candidate;
            }

            return baseCode + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
        }
    }
}
