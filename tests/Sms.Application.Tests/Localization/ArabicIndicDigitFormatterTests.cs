using Sms.Application.Localization;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Localization
{
    public class ArabicIndicDigitFormatterTests
    {
        [Fact]
        public void Converts_western_digits_to_arabic_indic()
        {
            Assert.Equal("٠١٢٣٤٥٦٧٨٩", ArabicIndicDigitFormatter.ToArabicIndicDigits("0123456789"));
        }

        [Fact]
        public void Leaves_non_digit_characters_untouched()
        {
            Assert.Equal("RCP-٢٠٢٦/٠٠٠١١٧", ArabicIndicDigitFormatter.ToArabicIndicDigits("RCP-2026/000117"));
        }

        [Fact]
        public void Round_trips_back_to_western_digits()
        {
            var arabicIndic = ArabicIndicDigitFormatter.ToArabicIndicDigits("STU-26-00042");
            Assert.Equal("STU-26-00042", ArabicIndicDigitFormatter.ToWesternDigits(arabicIndic));
        }
    }
}
