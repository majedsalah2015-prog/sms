using Sms.Application.Timetable;
using Sms.Domain.Timetable;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Timetable
{
    public class TimetableVersionStatusTransitionsTests
    {
        [Theory]
        [InlineData(TimetableVersionStatus.Draft, TimetableVersionStatus.Validated)]
        [InlineData(TimetableVersionStatus.Validated, TimetableVersionStatus.Published)]
        [InlineData(TimetableVersionStatus.Validated, TimetableVersionStatus.Draft)]
        [BusinessRule("BR-TTB-002")]
        public void Legal_moves_are_allowed(TimetableVersionStatus from, TimetableVersionStatus to)
        {
            Assert.True(TimetableVersionStatusTransitions.CanTransition(from, to));
        }

        [Theory]
        [InlineData(TimetableVersionStatus.Draft, TimetableVersionStatus.Published)]
        [InlineData(TimetableVersionStatus.Published, TimetableVersionStatus.Draft)]
        [InlineData(TimetableVersionStatus.Published, TimetableVersionStatus.Validated)]
        [BusinessRule("BR-TTB-002")]
        public void Illegal_moves_are_rejected(TimetableVersionStatus from, TimetableVersionStatus to)
        {
            Assert.False(TimetableVersionStatusTransitions.CanTransition(from, to));
        }
    }
}
