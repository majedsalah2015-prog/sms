using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sms.Application.Lookups
{
    /// <summary>
    /// Resolves a free-text cell from somebody else's register — an imported Access column
    /// reading "الثانوية العامة" or "Bachelors" — to a value in this school's lookup
    /// catalogue (doc/Modules/01 §9, BR-SET-001).
    /// <para>
    /// It exists because an import cannot ask. The catalogue is a set of ids; the old register
    /// holds whatever the last person typed, in either language, with or without ال, with or
    /// without a تاء مربوطة, sometimes with a هاء instead. Comparing those two with
    /// <c>string.Equals</c> matches almost nothing, and matching almost nothing means a
    /// thousand students imported with no qualification recorded at all.
    /// </para>
    /// <para>
    /// It refuses rather than guesses. A cell it cannot place returns null, and every caller
    /// here shows that to the operator before the import runs — an unmatched qualification is
    /// a blank field somebody fills in later, while a wrongly matched one is a wrong fact that
    /// nobody will ever look at again.
    /// </para>
    /// </summary>
    public static class LookupTextMatcher
    {
        /// <summary>Below this, a containment match is coincidence rather than evidence.</summary>
        private const int MinimumContainmentLength = 3;

        /// <summary>
        /// The id of the value this text names, or null when nothing in the catalogue answers to it.
        /// An exact match wins; failing that, the longest value name that contains the cell or is
        /// contained by it, so "الثانوية العامة" reaches "ثانوي" and a two-letter accident does not
        /// reach anything.
        /// </summary>
        public static int? Match(string? text, IReadOnlyCollection<(int Id, string Ar, string En)> values)
        {
            var needle = Normalize(text);
            if (needle.Length == 0 || values.Count == 0)
            {
                return null;
            }

            foreach (var value in values)
            {
                if (Normalize(value.Ar) == needle || Normalize(value.En) == needle)
                {
                    return value.Id;
                }
            }

            var best = (Id: (int?)null, Length: 0);
            foreach (var value in values)
            {
                foreach (var candidate in new[] { Normalize(value.Ar), Normalize(value.En) })
                {
                    if (candidate.Length < MinimumContainmentLength)
                    {
                        continue;
                    }

                    if ((needle.Contains(candidate, StringComparison.Ordinal) || candidate.Contains(needle, StringComparison.Ordinal))
                        && candidate.Length > best.Length)
                    {
                        best = (value.Id, candidate.Length);
                    }
                }
            }

            return best.Id;
        }

        /// <summary>
        /// One spelling for text that has several. Case and surrounding space go; so do the
        /// diacritics, the tatweel, and the four alif shapes, the alif maqsura and the taa
        /// marbuta — the letters an Arabic typist chooses without meaning to distinguish
        /// anything by the choice.
        /// </summary>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            var lastWasSpace = true;
            foreach (var raw in text.Trim().ToLowerInvariant())
            {
                var c = raw switch
                {
                    'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                    'ى' => 'ي',
                    'ة' => 'ه',
                    'ئ' => 'ي',
                    'ؤ' => 'و',
                    _ => raw,
                };

                // Tashkeel and tatweel carry no distinction anybody typing a qualification intended.
                if (c == 'ـ' || (c >= 'ً' && c <= 'ْ'))
                {
                    continue;
                }

                if (char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '/')
                {
                    if (!lastWasSpace)
                    {
                        builder.Append(' ');
                        lastWasSpace = true;
                    }

                    continue;
                }

                if (char.IsPunctuation(c) && !char.IsLetterOrDigit(c))
                {
                    continue;
                }

                builder.Append(c);
                lastWasSpace = false;
            }

            return builder.ToString().Trim();
        }

        /// <summary>The reader's name for a matched id, or null — used to show a match in a preview before it is committed.</summary>
        public static string? Display(int? id, IReadOnlyCollection<(int Id, string Ar, string En)> values, bool isArabic)
        {
            if (id == null)
            {
                return null;
            }

            var hit = values.FirstOrDefault(v => v.Id == id.Value);
            return hit == default ? null : (isArabic ? hit.Ar : hit.En);
        }
    }
}
