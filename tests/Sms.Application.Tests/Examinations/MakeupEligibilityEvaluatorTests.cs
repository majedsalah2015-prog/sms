using Sms.Application.Examinations;
using Sms.Domain.Attendance;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Examinations
{
    public class MakeupEligibilityEvaluatorTests
    {
        [Theory]
        [InlineData(AttendanceStatus.AbsentExcused, true)]
        [InlineData(AttendanceStatus.MedicalLeave, true)]
        [InlineData(AttendanceStatus.AbsentUnexcused, false)]
        [InlineData(AttendanceStatus.Present, false)]
        [BusinessRule("BR-EXM-008")]
        public void IsSystemEligible_matches_excused_and_medical_only(AttendanceStatus status, bool expected)
        {
            Assert.Equal(expected, MakeupEligibilityEvaluator.IsSystemEligible(status));
        }
    }
}
