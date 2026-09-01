using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Common
{
    /// <summary>
    /// Turns one written name into the four parts this product stores — الاسم الرباعي:
    /// first, father, grandfather, family (doc/Modules/12 §7, E-202).
    /// <para>
    /// Every register a school hands over writes the name as a single cell, and every screen here
    /// stores it in four columns. Splitting on spaces alone is not enough to bridge that, for two
    /// reasons this class exists to handle:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>The family name is the last word, not the fourth.</b> A three-word name is a first
    ///     name, a father and a family — putting "الخطيب" in the father slot and leaving the family
    ///     empty loses the one part every list and every certificate is sorted and printed by. A
    ///     five-word name has an extra ancestor in the middle, not an extra surname at the end.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Some names are two words.</b> "عبد الله" and "أبو زيد" are one part written with a
    ///     space in it. Counting them as two shifts every later part one slot to the left, which is
    ///     the difference between a correct record and a plausible-looking wrong one.
    ///   </description></item>
    /// </list>
    /// <para>
    /// Pure and static, per the engine convention: it takes text in and returns the decision out,
    /// so the import preview and the import itself can call the same code and the operator reads
    /// exactly what will be written.
    /// </para>
    /// </summary>
    public static class PersonNameSplitter
    {
        /// <summary>
        /// Words that are never a name on their own — they open one and belong with the word that
        /// follows them.
        /// <para>
        /// Arabic first, since that is what the registers are written in. The Latin list carries
        /// only the transliterations of these same particles, and deliberately not "Al" or "El":
        /// standalone "Al" is a given name in English ("Al Smith"), and merging it would turn a
        /// two-word name into an unimportable one. Transliterated Arabic writes those attached
        /// anyway ("Al-Khatib"), which arrives here as a single word and needs nothing.
        /// </para>
        /// </summary>
        private static readonly HashSet<string> Particles = new(StringComparer.OrdinalIgnoreCase)
        {
            "عبد", "عبدال", "أبو", "ابو", "أبا", "ابا", "آل", "ال", "بن", "ابن", "بنت", "أم", "ام", "ذو", "ذي",
            "abd", "abdel", "abdul", "abu", "bin", "ibn", "bint",
        };

        /// <summary>
        /// The characters an Arabic spreadsheet export sprinkles through a cell to control how it
        /// is drawn — bidi marks, zero-width joiners, the odd stray BOM. They are invisible in
        /// Excel and would otherwise become part of a stored name, where they break every later
        /// comparison of it against the same name typed by hand.
        /// </summary>
        private static bool IsInvisible(char c) =>
            c == 0xFEFF                       // a byte-order mark that rode in on a paste
            || (c >= 0x200B && c <= 0x200F)   // zero-width space and joiners, LRM, RLM
            || (c >= 0x202A && c <= 0x202E)   // bidi embedding and override
            || (c >= 0x2066 && c <= 0x2069);  // bidi isolates

        /// <summary>One name, in the four parts the record keeps it in. Missing parts are empty, never null.</summary>
        public sealed record Parts(string First, string Father, string Grandfather, string Family)
        {
            /// <summary>Nothing readable in the cell.</summary>
            public static readonly Parts Empty = new(string.Empty, string.Empty, string.Empty, string.Empty);

            /// <summary>
            /// A name this product can store: it has someone's own name and the family's. The two
            /// middle parts are genuinely optional — plenty of registers hold three-word names, and
            /// refusing those would refuse the person over their grandfather.
            /// </summary>
            public bool IsComplete => First.Length > 0 && Family.Length > 0;
        }

        /// <summary>
        /// Splits <paramref name="whole"/> into its four parts.
        /// <para>
        /// One word becomes a first name with no family — deliberately incomplete rather than
        /// guessed at, so the row is refused visibly instead of being stored under half a name.
        /// Two become first and family; three add the father; four are the quad name as written;
        /// five or more put the surplus in the grandfather's place, because the extra words in a
        /// long Arabic name are further ancestors and the family name stays the last one.
        /// </para>
        /// </summary>
        public static Parts Split(string? whole)
        {
            var words = Words(whole);
            if (words.Count == 0) { return Parts.Empty; }

            return words.Count switch
            {
                1 => new Parts(words[0], string.Empty, string.Empty, string.Empty),
                2 => new Parts(words[0], string.Empty, string.Empty, words[1]),
                3 => new Parts(words[0], words[1], string.Empty, words[2]),
                4 => new Parts(words[0], words[1], words[2], words[3]),
                _ => new Parts(
                    words[0],
                    words[1],
                    string.Join(" ", words.Skip(2).Take(words.Count - 3)),
                    words[words.Count - 1]),
            };
        }

        /// <summary>
        /// The name as words, with each opening particle joined to what it opens.
        /// <para>
        /// A particle left dangling at the end — a cell reading "محمد عبد" — is kept as its own
        /// word rather than dropped: it is somebody's data, truncated in the file it came from,
        /// and swallowing it here would hide that.
        /// </para>
        /// </summary>
        private static List<string> Words(string? whole)
        {
            if (string.IsNullOrWhiteSpace(whole)) { return new List<string>(); }

            var cleaned = new string(whole.Where(c => !IsInvisible(c)).ToArray());
            var raw = cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            var words = new List<string>(raw.Length);
            for (var i = 0; i < raw.Length; i++)
            {
                if (Particles.Contains(raw[i]) && i + 1 < raw.Length)
                {
                    words.Add(raw[i] + " " + raw[i + 1]);
                    i++;
                    continue;
                }

                words.Add(raw[i]);
            }

            return words;
        }
    }
}
