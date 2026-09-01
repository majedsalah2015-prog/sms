using System.Globalization;
using Sms.Application.Employees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Employees
{
    /// <summary>
    /// BR-EMP-004's المعدل, and the bilingual rule that surrounds it.
    /// <para>
    /// These exist because of a defect the qualifications screen shipped with for one afternoon:
    /// the average was bound as a <c>decimal?</c>, MVC binds simple types with
    /// <c>CurrentCulture</c>, and under Arabic the value an
    /// <c>&lt;input type="number"&gt;</c> posts stopped parsing — so a GPA the registrar typed was
    /// saved as "not recorded", under a success message. Verified live before the fix: 87.40
    /// stored under en-US, 3.81 stored NULL under ar-SA.
    /// </para>
    /// </summary>
    public class GradePointAverageReaderTests
    {
        private static readonly CultureInfo Arabic = new("ar-SA");
        private static readonly CultureInfo English = new("en-US");

        [Theory]
        [BusinessRule("BR-EMP-004")]
        [InlineData("3.81", 3.81)]
        [InlineData("87.40", 87.40)]
        [InlineData("70", 70)]
        [InlineData("  92.5  ", 92.5)]
        public void The_number_control_posts_the_invariant_form_and_it_reads_the_same_in_both_languages(string raw, decimal expected)
        {
            Assert.True(GradePointAverageReader.TryRead(raw, Arabic, out var underArabic));
            Assert.True(GradePointAverageReader.TryRead(raw, English, out var underEnglish));

            Assert.Equal(expected, underArabic);
            Assert.Equal(expected, underEnglish);
        }

        [Fact]
        [BusinessRule("BR-EMP-004")]
        public void A_blank_average_is_not_recorded_rather_than_refused()
        {
            // Optional field: the school's register frequently has the degree and not the mark.
            foreach (var raw in new[] { null, "", "   " })
            {
                Assert.True(GradePointAverageReader.TryRead(raw, Arabic, out var value));
                Assert.Null(value);
            }
        }

        [Fact]
        [BusinessRule("BR-EMP-004")]
        public void A_value_typed_with_the_readers_own_separator_is_still_read()
        {
            // The control posts invariant, but a pasted or hand-typed value need not. Whatever the
            // reader's culture calls a decimal separator is tried second.
            //
            // This is also the case that caught the first version of the fix: with
            // NumberStyles.Number the invariant attempt read "3,81" as 381 — a comma is its
            // thousands separator — and won before the reader's culture was ever asked.
            var german = new CultureInfo("de-DE");

            Assert.True(GradePointAverageReader.TryRead("3,81", german, out var value));
            Assert.Equal(3.81m, value);
        }

        [Fact]
        [BusinessRule("BR-EMP-004")]
        public void A_comma_under_a_point_culture_is_refused_rather_than_read_as_three_hundred_and_eighty_one()
        {
            // The guard on the above, and the reason thousands are not allowed. To an en-US reader
            // "3,81" is either 3.81 typed with the wrong separator or 381 typed with a stray comma,
            // and nothing here can tell which. Refusing puts a translated message in front of the
            // registrar; guessing puts 381 in a column that means "out of 4".
            Assert.False(GradePointAverageReader.TryRead("3,81", English, out var value));
            Assert.Null(value);
        }

        [Fact]
        [BusinessRule("BR-EMP-004")]
        public void Something_that_is_not_a_number_is_refused_rather_than_dropped()
        {
            // The whole point. Returning "no average" for an unreadable entry is what made the
            // original defect invisible: the save succeeded and the number was gone.
            Assert.False(GradePointAverageReader.TryRead("ممتاز", Arabic, out _));
            Assert.False(GradePointAverageReader.TryRead("n/a", English, out _));
        }

        [Theory]
        [BusinessRule("BR-EMP-004")]
        [InlineData(null, true)]
        [InlineData(0, true)]
        [InlineData(3.81, true)]
        [InlineData(100, true)]
        [InlineData(100.01, false)]
        [InlineData(-1, false)]
        public void The_range_spans_both_scales_because_the_certificate_decides_which_one(double? value, bool expected)
        {
            // Out of 4 and out of 100 both live in this column, unconverted, so the only bound
            // worth enforcing is the column's own.
            Assert.Equal(expected, GradePointAverageReader.IsInRange(value == null ? null : (decimal)value.Value));
        }
    }
}
