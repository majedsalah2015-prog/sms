using Sms.Application.Numbering;
using Sms.Domain.Numbering;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Numbering
{
    public class ResetKeyResolverTests
    {
        [Fact]
        [BusinessRule("BR-NUM-004")]
        public void Never_resolves_to_the_same_key_regardless_of_year()
        {
            var thisYear = ResetKeyResolver.Resolve(ResetPolicy.Never, "26", 2026);
            var nextYear = ResetKeyResolver.Resolve(ResetPolicy.Never, "27", 2027);

            Assert.Equal(ResetKeyResolver.LifelongKey, thisYear);
            Assert.Equal(thisYear, nextYear);
        }

        [Fact]
        [BusinessRule("BR-NUM-005")]
        public void Per_academic_year_partitions_by_the_academic_year_label()
        {
            Assert.Equal("26", ResetKeyResolver.Resolve(ResetPolicy.PerAcademicYear, "26", 2026));
            Assert.Equal("27", ResetKeyResolver.Resolve(ResetPolicy.PerAcademicYear, "27", 2026));
        }

        [Fact]
        [BusinessRule("BR-NUM-005")]
        public void Per_calendar_year_partitions_by_the_Gregorian_year()
        {
            Assert.Equal("2026", ResetKeyResolver.Resolve(ResetPolicy.PerCalendarYear, "26", 2026));
            Assert.Equal("2027", ResetKeyResolver.Resolve(ResetPolicy.PerCalendarYear, "26", 2027));
        }
    }
}
