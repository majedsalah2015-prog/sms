using System;
using System.Collections.Generic;
using Sms.Application.Lookups;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Lookups
{
    /// <summary>
    /// Reading a free-text cell out of a school's old Access register and placing it in this
    /// school's lookup catalogue (BR-SET-001). The catalogue below is the one
    /// <c>LookupProductSeedContributor</c> actually seeds for "EducationLevel", because a matcher
    /// that only works against invented values proves nothing about the import that uses it.
    /// </summary>
    public class LookupTextMatcherTests
    {
        private static readonly IReadOnlyCollection<(int Id, string Ar, string En)> Education = new[]
        {
            (1, "بدون", "None"),
            (2, "ابتدائي", "Primary"),
            (3, "إعدادي", "Preparatory"),
            (4, "ثانوي", "Secondary"),
            (5, "دبلوم", "Diploma"),
            (6, "بكالوريوس", "Bachelor"),
            (7, "ماجستير", "Master"),
            (8, "دكتوراه", "Doctorate"),
        };

        [Theory]
        [InlineData("ثانوي", 4)]
        [InlineData("بكالوريوس", 6)]
        [InlineData("Bachelor", 6)]
        [InlineData("  دبلوم  ", 5)]
        [BusinessRule("BR-SET-001")]
        public void A_cell_naming_a_value_exactly_finds_it(string cell, int expected)
        {
            Assert.Equal(expected, LookupTextMatcher.Match(cell, Education));
        }

        /// <summary>
        /// The shapes an Arabic typist chooses without meaning anything by the choice. A register
        /// where half the rows say "إعدادية" and half say "اعداديه" is not two qualifications.
        /// </summary>
        [Theory]
        [InlineData("الثانوية العامة", 4)]
        [InlineData("ثانويه", 4)]
        [InlineData("إعدادية", 3)]
        [InlineData("اعدادي", 3)]
        [InlineData("ابتدائية", 2)]
        [InlineData("دبلوم متوسط", 5)]
        [InlineData("Bachelors degree", 6)]
        [BusinessRule("BR-SET-001")]
        public void A_cell_spelling_a_value_its_own_way_still_finds_it(string cell, int expected)
        {
            Assert.Equal(expected, LookupTextMatcher.Match(cell, Education));
        }

        /// <summary>
        /// The whole point of the class. A qualification nothing in the catalogue answers to imports
        /// blank and is counted in the preview — the alternative, snapping it to the nearest value,
        /// writes a wrong fact that nobody ever looks at again.
        /// </summary>
        [Theory]
        [InlineData("أمي")]
        [InlineData("لا يقرأ ولا يكتب")]
        [InlineData("—")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [BusinessRule("BR-SET-001")]
        public void A_cell_nothing_answers_to_is_refused_rather_than_guessed(string? cell)
        {
            Assert.Null(LookupTextMatcher.Match(cell, Education));
        }

        [Fact]
        public void An_empty_catalogue_matches_nothing()
        {
            Assert.Null(LookupTextMatcher.Match("ثانوي", Array.Empty<(int, string, string)>()));
        }

        /// <summary>Two candidates both fit; the longer one is the one that says more.</summary>
        [Fact]
        public void The_longest_candidate_wins_a_containment_match()
        {
            var values = new[] { (1, "دبلوم", "Diploma"), (2, "دبلوم عالٍ", "Higher Diploma") };
            Assert.Equal(2, LookupTextMatcher.Match("دبلوم عالي متخصص", values));
        }

        [Theory]
        [InlineData("أحمد", "احمد")]
        [InlineData("إعدادية", "اعداديه")]
        [InlineData("  مُــدرِّس  ", "مدرس")]
        [InlineData("Bachelor  Degree", "bachelor degree")]
        public void Normalize_collapses_the_spellings_that_mean_the_same_thing(string raw, string expected)
        {
            Assert.Equal(expected, LookupTextMatcher.Normalize(raw));
        }

        [Fact]
        public void Display_names_a_match_in_the_readers_language_and_says_nothing_about_a_miss()
        {
            Assert.Equal("بكالوريوس", LookupTextMatcher.Display(6, Education, isArabic: true));
            Assert.Equal("Bachelor", LookupTextMatcher.Display(6, Education, isArabic: false));
            Assert.Null(LookupTextMatcher.Display(null, Education, isArabic: true));
            Assert.Null(LookupTextMatcher.Display(99, Education, isArabic: true));
        }
    }
}
