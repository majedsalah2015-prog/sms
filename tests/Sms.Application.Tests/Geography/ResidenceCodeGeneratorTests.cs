using System;
using System.Linq;
using Sms.Application.Geography;
using Xunit;

namespace Sms.Application.Tests.Geography
{
    /// <summary>
    /// The stable key a residence row gets when nobody types one. It is what the seeder is
    /// idempotent on and what the unique index is built over, so "whatever the operator typed" was
    /// never an option — and a required box marked "code" is answered with "1".
    /// </summary>
    public class ResidenceCodeGeneratorTests
    {
        [Fact]
        public void A_name_becomes_an_upper_case_ascii_code()
        {
            Assert.Equal("KHANYUNIS", ResidenceCodeGenerator.Next("Khan Yunis", Array.Empty<string>()));
        }

        [Fact]
        public void Punctuation_and_spacing_are_dropped_rather_than_transliterated()
        {
            // The code travels into exports, import mappings and URLs, so it stays letters and digits.
            Assert.Equal("DEIRALBALAH", ResidenceCodeGenerator.Next("Deir Al-Balah", Array.Empty<string>()));
        }

        [Fact]
        public void A_collision_takes_the_next_number_rather_than_failing()
        {
            var code = ResidenceCodeGenerator.Next("Gaza", new[] { "GAZA", "GAZA2" });

            Assert.Equal("GAZA3", code);
        }

        [Fact]
        public void A_code_already_taken_in_a_different_case_still_counts_as_taken()
        {
            Assert.Equal("GAZA2", ResidenceCodeGenerator.Next("Gaza", new[] { "gaza" }));
        }

        [Fact]
        public void A_name_with_nothing_ascii_in_it_still_yields_a_code()
        {
            // The English name is required by the form, but an operator who pastes the Arabic into
            // both boxes must not bring down the save — the row still needs a key.
            var code = ResidenceCodeGenerator.Next("حي النصر", Array.Empty<string>());

            Assert.False(string.IsNullOrWhiteSpace(code));
            Assert.All(code, ch => Assert.True(char.IsLetterOrDigit(ch) && ch < 128));
        }

        [Fact]
        public void A_long_name_is_cut_short_enough_to_leave_room_for_a_suffix()
        {
            // The column holds 20; the base stops at 16 so a collision still fits.
            var code = ResidenceCodeGenerator.Next(new string('A', 40), Array.Empty<string>());

            Assert.Equal(16, code.Length);
        }
    }
}
