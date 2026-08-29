using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Lookups;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Lookups
{
    /// <summary>
    /// The stable key a lookup value gets when the person authoring the catalogue did not supply
    /// one (BR-SET-001). What matters here is that the result is usable as an identity — ASCII,
    /// bounded, and never one another value already holds (BR-SET-002).
    /// </summary>
    public class LookupCodeGeneratorTests
    {
        private static readonly string[] Nothing = Array.Empty<string>();

        [Theory]
        [InlineData("Bachelor", "BACHELOR")]
        [InlineData("An-Najah National University", "ANNAJAHNATIONALU")]
        [InlineData("Bank of Palestine", "BANKOFPALESTINE")]
        [InlineData("bachelor", "BACHELOR")]
        public void Derives_an_upper_case_ascii_code_from_the_name(string name, string expected)
            => Assert.Equal(expected, LookupCodeGenerator.FromName(name, Nothing));

        [Fact]
        public void Caps_the_code_so_it_stays_a_column_rather_than_a_sentence()
        {
            var code = LookupCodeGenerator.FromName("Faculty of Engineering and Information Technology", Nothing);
            Assert.Equal(16, code.Length);
        }

        [BusinessRule("BR-SET-002")]
        [Fact]
        public void Never_returns_a_code_another_value_already_holds()
        {
            // The code is the identity other rows point at. Handing back one that is taken would
            // either overwrite a different university or die on the unique index.
            var taken = new[] { "BACHELOR", "BACHELOR2" };
            Assert.Equal("BACHELOR3", LookupCodeGenerator.FromName("Bachelor", taken));
        }

        [Fact]
        public void Treats_an_existing_code_as_taken_whatever_its_case()
        {
            // The store keys the pair (category, code) case-insensitively, so offering "BACHELOR"
            // as free because the row holds "bachelor" would fail only on save.
            Assert.NotEqual("BACHELOR", LookupCodeGenerator.FromName("Bachelor", new[] { "bachelor" }));
        }

        [Fact]
        public void Keeps_a_long_name_within_the_cap_once_it_has_a_suffix_too()
        {
            var first = LookupCodeGenerator.FromName("Faculty of Engineering and Information Technology", Nothing);
            var second = LookupCodeGenerator.FromName("Faculty of Engineering and Information Technology", new[] { first });

            Assert.NotEqual(first, second);
            Assert.True(second.Length <= 16, $"'{second}' is longer than the column allows.");
        }

        [Fact]
        public void Falls_back_to_the_prefix_when_the_name_is_arabic_only()
        {
            // A code made of Arabic letters is a correct string that exports, import mappings and
            // URLs render as question marks — so an Arabic name yields a dull key, not a broken one.
            var code = LookupCodeGenerator.FromName("جامعة النجاح الوطنية", Nothing, "University");

            Assert.Equal("UNIVERSITY2", code);
            Assert.All(code, c => Assert.True(c <= 127, $"'{code}' is not ASCII."));
        }

        [Fact]
        public void Keeps_falling_back_to_distinct_keys_for_a_whole_arabic_catalogue()
        {
            // The realistic case: a registrar types eighty universities in Arabic alone and leaves
            // the code box empty every time. Each must still get a key of its own.
            var taken = new List<string>();
            foreach (var _ in Enumerable.Range(0, 20))
            {
                var code = LookupCodeGenerator.FromName("جامعة", taken, "University");
                Assert.DoesNotContain(code, taken);
                taken.Add(code);
            }
        }

        [Fact]
        public void Falls_back_when_the_name_is_blank_or_punctuation()
        {
            Assert.Equal("BANK2", LookupCodeGenerator.FromName(null, Nothing, "Bank"));
            Assert.Equal("BANK2", LookupCodeGenerator.FromName("   ", Nothing, "Bank"));
            Assert.Equal("BANK2", LookupCodeGenerator.FromName("— …", Nothing, "Bank"));
        }

        [Fact]
        public void Falls_back_to_a_usable_key_even_when_the_prefix_is_unusable_too()
        {
            Assert.Equal("VAL2", LookupCodeGenerator.FromName("جامعة", Nothing, "الجامعة"));
        }
    }
}
