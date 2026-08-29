using System;
using System.Collections.Generic;
using System.Text;

namespace Sms.Application.Lookups
{
    /// <summary>
    /// Invents the stable machine key for a lookup value the operator did not supply one for
    /// (doc/Modules/01 §8, BR-SET-001).
    /// <para>
    /// It exists because <c>LookupValue.Code</c> is an identity — other rows point at it and it is
    /// never re-purposed once referenced (BR-SET-002) — while the person authoring a catalogue of
    /// eighty universities has no view about what any of them should be called internally. Made to
    /// type one, they type "1", "2", "3", and the codes stop distinguishing anything the day two
    /// catalogues are compared. Derived from the name, they read back.
    /// </para>
    /// <para>
    /// ASCII only, deliberately. The generated code is shown LTR beside a name that is usually
    /// Arabic, and it travels into exports, import mappings and URLs; a key made of Arabic letters
    /// would be a correct string that half of those paths render as question marks. A name with no
    /// ASCII in it falls back to the caller's prefix and a number — a dull key, which is the right
    /// answer when there is nothing to derive a good one from.
    /// </para>
    /// </summary>
    public static class LookupCodeGenerator
    {
        /// <summary>Long enough to stay readable, short enough for a grid column and an export header.</summary>
        private const int MaxLength = 16;

        /// <summary>The point past which a suffix search is a bug rather than a busy catalogue.</summary>
        private const int MaxAttempts = 1000;

        /// <summary>
        /// A code derived from <paramref name="name"/> that no entry of <paramref name="taken"/>
        /// already holds. Case-insensitive against the existing codes, because the store treats
        /// "NAJAH" and "najah" as one key and offering the second as free would fail on save.
        /// </summary>
        /// <param name="name">The value's name — the English one where a screen has both, since the result is ASCII.</param>
        /// <param name="taken">Every code already in this category, active or not: a retired value still owns its key.</param>
        /// <param name="fallbackPrefix">What to build on when the name yields no ASCII at all.</param>
        public static string FromName(string? name, IEnumerable<string> taken, string fallbackPrefix = "VAL")
        {
            var set = new HashSet<string>(taken ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var baseCode = Ascii(name);
            if (baseCode.Length == 0)
            {
                baseCode = Ascii(fallbackPrefix);
                if (baseCode.Length == 0)
                {
                    baseCode = "VAL";
                }

                // Nothing was derived, so the bare prefix is not a candidate on its own: the first
                // Arabic-only name in a category would take "BANK" and read as though it meant it.
                return Numbered(baseCode, set);
            }

            return set.Contains(baseCode) ? Numbered(baseCode, set) : baseCode;
        }

        /// <summary>The first of BASE2, BASE3 … that is free, trimmed so the suffix never pushes it past the limit.</summary>
        private static string Numbered(string baseCode, HashSet<string> taken)
        {
            for (var i = 2; i < MaxAttempts; i++)
            {
                var suffix = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var stem = baseCode.Length + suffix.Length > MaxLength
                    ? baseCode.Substring(0, MaxLength - suffix.Length)
                    : baseCode;
                var candidate = stem + suffix;
                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }

            // A thousand collisions on one stem is not a catalogue anybody is reading. Unique beats
            // pretty here, and the caller's save still refuses a duplicate if even this repeats.
            return baseCode.Substring(0, Math.Min(baseCode.Length, MaxLength - 4))
                + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
        }

        /// <summary>Upper-case ASCII letters and digits of <paramref name="text"/>, capped — everything else drops.</summary>
        private static string Ascii(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(MaxLength);
            foreach (var c in text.ToUpperInvariant())
            {
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    builder.Append(c);
                    if (builder.Length == MaxLength)
                    {
                        break;
                    }
                }
            }

            return builder.ToString();
        }
    }
}
