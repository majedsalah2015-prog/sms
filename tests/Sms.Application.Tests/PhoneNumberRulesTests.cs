using Sms.Application.Notifications;
using Xunit;

namespace Sms.Application.Tests
{
    /// <summary>
    /// The normalisation every WhatsApp and SMS send depends on (BR-NOT-009). These are the
    /// forms a school's parent register actually holds — the point of the engine is that all
    /// of them reach the same person.
    /// </summary>
    public class PhoneNumberRulesTests
    {
        [Theory]
        [InlineData("+970599123456")]
        [InlineData("00970599123456")]
        [InlineData("+970 59 912 3456")]
        [InlineData("+970-599-123-456")]
        [InlineData("(+970) 599 123 456")]
        public void Every_international_form_of_one_number_normalises_to_the_same_thing(string entered)
            => Assert.Equal("+970599123456", PhoneNumberRules.Normalize(entered).E164);

        [Fact]
        public void A_national_format_number_is_completed_with_the_schools_dialling_code()
        {
            // The commonest thing in a register: a leading trunk zero and no country.
            Assert.Equal("+970599123456", PhoneNumberRules.Normalize("0599123456", "970").E164);
            Assert.Equal("+970599123456", PhoneNumberRules.Normalize("0599 123 456", "+970").E164);
        }

        [Fact]
        public void Arabic_indic_digits_are_read_as_digits()
        {
            // What an Arabic keyboard produces by default. A parent whose number was typed this
            // way is not less reachable than one whose was not.
            Assert.Equal("+970599123456", PhoneNumberRules.Normalize("٠٥٩٩١٢٣٤٥٦", "970").E164);
            Assert.Equal("+970599123456", PhoneNumberRules.Normalize("۰۵۹۹۱۲۳۴۵۶", "970").E164);
        }

        [Fact]
        public void A_national_format_number_with_no_dialling_code_is_refused_rather_than_guessed()
        {
            // Guessing a country delivers a family's message to a stranger, which is worse than
            // not sending it.
            var result = PhoneNumberRules.Normalize("0599123456");

            Assert.False(result.IsValid);
            Assert.Equal(PhoneNumberRules.Rejection.NeedsCountryCode, result.Rejection);
        }

        [Fact]
        public void A_number_that_already_carries_the_dialling_code_is_not_given_it_twice()
            => Assert.Equal("+970599123456", PhoneNumberRules.Normalize("970599123456", "970").E164);

        [Theory]
        [InlineData("", PhoneNumberRules.Rejection.Empty)]
        [InlineData("   ", PhoneNumberRules.Rejection.Empty)]
        [InlineData("not a number", PhoneNumberRules.Rejection.NoDigits)]
        [InlineData("+12345", PhoneNumberRules.Rejection.TooShort)]
        [InlineData("+1234567890123456789", PhoneNumberRules.Rejection.TooLong)]
        public void What_cannot_be_sent_to_says_why(string entered, PhoneNumberRules.Rejection expected)
        {
            var result = PhoneNumberRules.Normalize(entered, "970");

            Assert.False(result.IsValid);
            Assert.Null(result.E164);
            Assert.Equal(expected, result.Rejection);
        }

        [Fact]
        public void Masking_leaves_only_the_last_four_digits()
        {
            // The delivery log answers "did it go out", not "what is this family's number".
            Assert.Equal("••••••••3456", PhoneNumberRules.Mask("+970599123456"));
            Assert.Equal(string.Empty, PhoneNumberRules.Mask(null));
        }
    }
}
