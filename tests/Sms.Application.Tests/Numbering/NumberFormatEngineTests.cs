using Sms.Application.Numbering;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Numbering
{
    public class NumberFormatEngineTests
    {
        [Fact]
        [BusinessRule("BR-NUM-001")]
        public void Renders_the_doc_08_student_number_example()
        {
            var context = new NumberFormatContext(schoolCode: "AND", academicYearLabel: "26", gregorianYear: 2026, sequence: 42);

            var result = NumberFormatEngine.Render("STU-{YEAR}-{SEQ:5}", context);

            Assert.Equal("STU-26-00042", result);
        }

        [Fact]
        [BusinessRule("BR-NUM-001")]
        public void Renders_the_doc_08_receipt_number_example()
        {
            var context = new NumberFormatContext(schoolCode: "AND", academicYearLabel: "26", gregorianYear: 2026, sequence: 117);

            var result = NumberFormatEngine.Render("RCP/{SCHOOL}/{GYEAR}/{SEQ:6}", context);

            Assert.Equal("RCP/AND/2026/000117", result);
        }

        [Fact]
        [BusinessRule("BR-NUM-007")]
        public void The_sequence_is_zero_padded_to_the_configured_width_using_invariant_digits()
        {
            var context = new NumberFormatContext("AND", "26", 2026, sequence: 7);

            var result = NumberFormatEngine.Render("{SEQ:3}", context);

            Assert.Equal("007", result);
        }

        [Fact]
        [BusinessRule("BR-NUM-001")]
        public void A_sequence_wider_than_the_padding_is_never_truncated()
        {
            var context = new NumberFormatContext("AND", "26", 2026, sequence: 123456);

            var result = NumberFormatEngine.Render("{SEQ:3}", context);

            Assert.Equal("123456", result);
        }
    }
}
