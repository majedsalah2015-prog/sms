using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sms.Application.Sections
{
    /// <summary>
    /// Pure BR-SCN-001: the next names in a grade's section series. The rule says
    /// "naming follows a school pattern (e.g. {GradeCode}-{A,B,C} or bilingual names
    /// like خامس-أ); names unique within grade+year" — a pattern, not a fixed list,
    /// so this reads the pattern off what the grade already has rather than
    /// imposing one.
    /// <para>
    /// <b>Why it continues rather than fills.</b> Given أ and ج it proposes د, not
    /// the gap at ب. A missing letter in a school's series is usually a section that
    /// was closed, and its name is still taken — <c>DefineSectionAsync</c> would
    /// refuse the duplicate, and even where it would not, reusing the name of a
    /// section that had students in it makes two different groups share one label in
    /// the year's records.
    /// </para>
    /// <para>
    /// Both halves of the pair advance together: the Arabic letter and the Latin one
    /// are the same position in their own alphabets, so أ pairs with A and the fifth
    /// section reads هـ / E. A school that numbers instead gets numbers on both
    /// sides, because a section called "2-1" in English and "٢-أ" in Arabic is one
    /// section with two different names.
    /// </para>
    /// </summary>
    public static class SectionNameSequence
    {
        /// <summary>
        /// The letters schools actually label sections with, in the order they use
        /// them. Ten is not a limit anyone reaches — the largest grade in a school
        /// this product is built for runs to six or seven sections — and beyond it
        /// the sequence falls back to numbering rather than inventing a letter.
        /// </summary>
        private static readonly string[] ArabicLetters = { "أ", "ب", "ج", "د", "هـ", "و", "ز", "ح", "ط", "ي" };

        private static readonly string[] LatinLetters = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };

        /// <summary>What a school puts between the grade and the section in a name.</summary>
        private static readonly char[] Separators = { '-', '/', '_', ' ' };

        /// <summary>How a grade labels its sections, read from the names it already has.</summary>
        public enum Style
        {
            /// <summary>أ · ب · ج paired with A · B · C — the doc's first example and the default for an empty grade.</summary>
            Letters = 1,

            /// <summary>1 · 2 · 3 on both sides, for a school that numbers.</summary>
            Numbers = 2,
        }

        /// <summary>One proposed section, named on both sides.</summary>
        public sealed record ProposedName(string NameAr, string NameEn);

        /// <summary>
        /// Reads the style off the existing names. A grade with nothing in it yet gets
        /// <see cref="Style.Letters"/>; a grade whose newest section ends in a digit
        /// keeps numbering. The <em>last</em> name decides rather than a majority vote:
        /// a school that switched conventions means the switch, and the alternative is
        /// telling it that its own most recent decision was outvoted by history.
        /// </summary>
        public static Style DetectStyle(IReadOnlyCollection<string> existingEnglishNames)
        {
            var suffix = existingEnglishNames
                .Select(SuffixOf)
                .LastOrDefault(s => !string.IsNullOrEmpty(s));

            return suffix != null && suffix.All(char.IsDigit) ? Style.Numbers : Style.Letters;
        }

        /// <summary>
        /// The next <paramref name="count"/> names for a grade, continuing past
        /// whatever it already holds.
        /// <para>
        /// <b>The prefix comes from the grade's own sections where it has any.</b>
        /// BR-SCN-001 calls the naming "a school pattern", and the prefix is half of
        /// that pattern: a grade whose sections read "1-A" and "1-B" is a grade that
        /// calls itself "1", whatever its full name in the ladder is. Only an empty
        /// grade falls back to <paramref name="gradeFallbackAr"/> /
        /// <paramref name="gradeFallbackEn"/> — and passing those empty yields bare
        /// suffixes, which is what a school naming sections "أ" rather than "خامس-أ"
        /// wants.
        /// </para>
        /// </summary>
        public static IReadOnlyList<ProposedName> Next(
            string gradeFallbackAr,
            string gradeFallbackEn,
            IReadOnlyCollection<ExistingName> existing,
            int count,
            Style? style = null)
        {
            if (count <= 0)
            {
                return Array.Empty<ProposedName>();
            }

            var englishNames = existing.Select(e => e.NameEn).ToList();
            var chosen = style ?? DetectStyle(englishNames);
            var startIndex = NextIndex(englishNames, chosen);

            var last = existing.LastOrDefault();
            var ar0 = last == null ? new Shape(gradeFallbackAr, DefaultSeparator) : ShapeOf(last.NameAr, gradeFallbackAr);
            var en0 = last == null ? new Shape(gradeFallbackEn, DefaultSeparator) : ShapeOf(last.NameEn, gradeFallbackEn);

            var names = new List<ProposedName>(count);
            for (var i = 0; i < count; i++)
            {
                var index = startIndex + i;
                var (ar, en) = chosen == Style.Numbers || index >= LatinLetters.Length
                    ? SuffixPair(index)
                    : (ArabicLetters[index], LatinLetters[index]);

                names.Add(new ProposedName(ar0.Join(ar), en0.Join(en)));
            }

            return names;
        }

        /// <summary>A section the grade already has, in both languages.</summary>
        public sealed record ExistingName(string NameAr, string NameEn);

        private const string DefaultSeparator = "-";

        /// <summary>
        /// How this grade writes a section name: what comes before the suffix, and what
        /// joins the two. Both are copied rather than re-derived — a school writing
        /// "أول أ" with a space means the space, and proposing "أول-ب" next to it would
        /// be the tool breaking the convention it exists to follow.
        /// </summary>
        private sealed record Shape(string Prefix, string Separator)
        {
            public string Join(string suffix)
                => string.IsNullOrWhiteSpace(Prefix) ? suffix : $"{Prefix}{Separator}{suffix}";
        }

        /// <summary>
        /// Reads the shape off an existing name — "الصف الأول-ب" gives ("الصف الأول", "-"),
        /// "أول أ" gives ("أول", " "), and a bare "ب" gives the fallback with the default
        /// separator, because a name with no prefix says nothing about how one would join.
        /// </summary>
        private static Shape ShapeOf(string? name, string fallbackPrefix)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return new Shape(fallbackPrefix, DefaultSeparator);
            }

            var trimmed = name.Trim();
            var cut = trimmed.LastIndexOfAny(Separators);
            return cut <= 0
                ? new Shape(string.Empty, DefaultSeparator)
                : new Shape(trimmed[..cut].TrimEnd(), trimmed[cut].ToString());
        }

        /// <summary>
        /// Where the series has reached. Positions are read from the English half
        /// because it is the half with a stable alphabet — an Arabic name may be
        /// spelled هـ or ه, and both mean the fifth section.
        /// </summary>
        private static int NextIndex(IReadOnlyCollection<string> existingEnglishNames, Style style)
        {
            var highest = -1;
            foreach (var name in existingEnglishNames)
            {
                var suffix = SuffixOf(name);
                if (string.IsNullOrEmpty(suffix))
                {
                    continue;
                }

                var position = style == Style.Numbers || suffix.All(char.IsDigit)
                    ? (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n - 1 : -1)
                    : Array.FindIndex(LatinLetters, l => string.Equals(l, suffix, StringComparison.OrdinalIgnoreCase));

                highest = Math.Max(highest, position);
            }

            return highest + 1;
        }

        /// <summary>
        /// The part after the last separator — "1-B" gives "B", "Grade 5 / C" gives
        /// "C", a bare "B" gives itself. Everything else in the name is the grade,
        /// and the grade is not what advances.
        /// </summary>
        private static string SuffixOf(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var trimmed = name.Trim();
            var cut = trimmed.LastIndexOfAny(Separators);
            return cut < 0 ? trimmed : trimmed[(cut + 1)..].Trim();
        }

        private static (string Ar, string En) SuffixPair(int index)
        {
            var number = (index + 1).ToString(CultureInfo.InvariantCulture);
            return (number, number);
        }

    }
}
