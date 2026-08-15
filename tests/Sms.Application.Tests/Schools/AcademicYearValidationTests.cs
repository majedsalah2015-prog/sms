using System;
using Sms.Application.Schools;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Schools
{
    public class AcademicYearValidationTests
    {
        [Fact]
        [BusinessRule("BR-AYR-001")]
        public void A_typical_school_year_span_is_valid()
        {
            Assert.True(AcademicYearValidation.HasValidSpan(new DateTime(2026, 9, 1), new DateTime(2027, 6, 30)));
        }

        [Fact]
        [BusinessRule("BR-AYR-001")]
        public void A_span_under_6_months_is_rejected()
        {
            Assert.False(AcademicYearValidation.HasValidSpan(new DateTime(2026, 9, 1), new DateTime(2027, 1, 1)));
        }

        [Fact]
        [BusinessRule("BR-AYR-001")]
        public void A_span_over_14_months_is_rejected()
        {
            Assert.False(AcademicYearValidation.HasValidSpan(new DateTime(2026, 9, 1), new DateTime(2028, 1, 1)));
        }

        [Fact]
        [BusinessRule("BR-AYR-001")]
        public void An_end_date_before_the_start_date_is_rejected()
        {
            Assert.False(AcademicYearValidation.HasValidSpan(new DateTime(2026, 9, 1), new DateTime(2026, 8, 1)));
        }

        [Theory]
        [InlineData("2026-09-01", "2027-06-30", "2027-01-01", "2027-12-31", true)] // mid-overlap
        [InlineData("2026-09-01", "2027-06-30", "2027-07-01", "2028-06-30", false)] // back-to-back, no overlap
        [InlineData("2026-09-01", "2027-06-30", "2025-09-01", "2026-09-01", false)] // touches at the boundary day — not an overlap
        [BusinessRule("BR-AYR-001")]
        public void Overlap_detection(string s1, string e1, string s2, string e2, bool expectedOverlap)
        {
            Assert.Equal(expectedOverlap, AcademicYearValidation.Overlaps(
                DateTime.Parse(s1), DateTime.Parse(e1), DateTime.Parse(s2), DateTime.Parse(e2)));
        }
    }
}
