using System;
using System.Linq;
using Sms.Application.Sections;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Sections
{
    /// <summary>
    /// BR-SCN-001 calls section naming "a school pattern", not one pattern, and the
    /// demo data already carries both of the doc's examples — 1-A and 2-1. So the
    /// tests are about reading a grade's own convention off what it holds: its
    /// letters or numbers, its own prefix, and never a name it has used before.
    /// </summary>
    public class SectionNameSequenceTests
    {
        private static SectionNameSequence.ExistingName[] None => Array.Empty<SectionNameSequence.ExistingName>();

        private static SectionNameSequence.ExistingName[] Existing(params (string Ar, string En)[] names)
            => names.Select(n => new SectionNameSequence.ExistingName(n.Ar, n.En)).ToArray();

        [Fact]
        [BusinessRule("BR-SCN-001")]
        public void A_grade_with_no_sections_starts_at_the_first_letter()
        {
            var names = SectionNameSequence.Next("أول", "Grade 1", None, 3);

            Assert.Equal(new[] { "أول-أ", "أول-ب", "أول-ج" }, names.Select(n => n.NameAr).ToArray());
            Assert.Equal(new[] { "Grade 1-A", "Grade 1-B", "Grade 1-C" }, names.Select(n => n.NameEn).ToArray());
        }

        [Fact]
        [BusinessRule("BR-SCN-001")]
        public void A_lettered_grade_continues_its_letters()
        {
            var names = SectionNameSequence.Next("أول", "Grade 1", Existing(("أول-أ", "1-A"), ("أول-ب", "1-B")), 2);

            Assert.Equal(new[] { "1-C", "1-D" }, names.Select(n => n.NameEn).ToArray());
            Assert.Equal(new[] { "أول-ج", "أول-د" }, names.Select(n => n.NameAr).ToArray());
        }

        /// <summary>The demo data's own second style: a school that numbers keeps numbering.</summary>
        [Fact]
        [BusinessRule("BR-SCN-001")]
        public void A_numbered_grade_keeps_numbering()
        {
            Assert.Equal(SectionNameSequence.Style.Numbers, SectionNameSequence.DetectStyle(new[] { "2-1" }));

            var names = SectionNameSequence.Next("ثاني", "Grade 2", Existing(("ثاني-1", "2-1"), ("ثاني-2", "2-2")), 2);

            Assert.Equal(new[] { "2-3", "2-4" }, names.Select(n => n.NameEn).ToArray());
            Assert.Equal(new[] { "ثاني-3", "ثاني-4" }, names.Select(n => n.NameAr).ToArray());
        }

        /// <summary>
        /// The prefix is half the pattern. A grade whose sections read "1-A" calls
        /// itself "1", whatever its full name in the ladder is — proposing
        /// "الصف الأول الابتدائي-ج" next to "أول-أ" would be the school's own
        /// convention broken by the tool meant to follow it.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-001")]
        public void The_grades_own_prefix_is_continued_not_its_full_name()
        {
            var names = SectionNameSequence.Next("الصف الأول الابتدائي", "Grade 1 Elementary", Existing(("أول-أ", "1-A")), 1);

            Assert.Equal("أول-ب", names.Single().NameAr);
            Assert.Equal("1-B", names.Single().NameEn);
        }

        /// <summary>
        /// A gap is a section that was closed, and its name is still taken. Filling it
        /// would either be refused as a duplicate or give two different groups the
        /// same label in one year's records.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-001")]
        public void A_gap_left_by_a_closed_section_is_not_filled()
        {
            var names = SectionNameSequence.Next("أول", "Grade 1", Existing(("أول-أ", "1-A"), ("أول-ج", "1-C")), 1);

            Assert.Equal("1-D", names.Single().NameEn);
        }

        /// <summary>
        /// The last name decides, not a majority: a school that switched conventions
        /// meant the switch, and outvoting its most recent decision with its history
        /// would be the wrong way round.
        /// </summary>
        [Fact]
        public void The_newest_section_decides_the_style()
        {
            Assert.Equal(SectionNameSequence.Style.Numbers, SectionNameSequence.DetectStyle(new[] { "3-A", "3-B", "3-3" }));
            Assert.Equal(SectionNameSequence.Style.Letters, SectionNameSequence.DetectStyle(new[] { "3-1", "3-B" }));
        }

        [Fact]
        public void A_school_that_names_sections_bare_gets_bare_names()
        {
            var names = SectionNameSequence.Next(string.Empty, string.Empty, Existing(("أ", "A")), 2);

            Assert.Equal(new[] { "B", "C" }, names.Select(n => n.NameEn).ToArray());
            Assert.Equal(new[] { "ب", "ج" }, names.Select(n => n.NameAr).ToArray());
        }

        /// <summary>
        /// Past the tenth section the letters run out. Numbering on is the honest
        /// answer; inventing an eleventh letter nobody labels a section with is not.
        /// </summary>
        [Fact]
        public void Past_the_letters_the_sequence_numbers_instead()
        {
            var existing = Enumerable.Range(0, 10)
                .Select(i => (Ar: $"أول-{i}", En: $"1-{(char)('A' + i)}"))
                .ToArray();

            var names = SectionNameSequence.Next("أول", "Grade 1", Existing(existing), 1);

            Assert.Equal("1-11", names.Single().NameEn);
        }

        /// <summary>
        /// A school writing "أول أ" with a space means the space. Proposing "أول-ب"
        /// next to it would be the tool breaking the convention it exists to follow —
        /// and it is the separator the demo data actually uses on the Arabic side.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-001")]
        public void The_separator_the_school_uses_is_the_one_that_comes_back()
        {
            var names = SectionNameSequence.Next("أول", "Grade 1", Existing(("أول أ", "1-A")), 1);

            Assert.Equal("أول ب", names.Single().NameAr);
            Assert.Equal("1-B", names.Single().NameEn);
        }

        [Fact]
        public void Asking_for_nothing_proposes_nothing()
        {
            Assert.Empty(SectionNameSequence.Next("أول", "Grade 1", Existing(("أول-أ", "1-A")), 0));
            Assert.Empty(SectionNameSequence.Next("أول", "Grade 1", Existing(("أول-أ", "1-A")), -3));
        }
    }
}
