using Sms.Application.Examinations;
using Sms.Domain.Attendance;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Examinations
{
    public class ExamAbsenceMarkPolicyTests
    {
        [Fact]
        [BusinessRule("BR-EXM-006")]
        public void Unexcused_absence_zeroes_the_mark_when_policy_enabled()
        {
            Assert.True(ExamAbsenceMarkPolicy.ShouldZeroMark(AttendanceStatus.AbsentUnexcused, unexcusedZeroPolicyEnabled: true));
        }

        [Fact]
        [BusinessRule("BR-EXM-006")]
        public void Unexcused_absence_never_zeroes_when_policy_disabled()
        {
            Assert.False(ExamAbsenceMarkPolicy.ShouldZeroMark(AttendanceStatus.AbsentUnexcused, unexcusedZeroPolicyEnabled: false));
        }

        [Fact]
        [BusinessRule("BR-EXM-006")]
        public void Excused_absence_is_never_zeroed()
        {
            Assert.False(ExamAbsenceMarkPolicy.ShouldZeroMark(AttendanceStatus.AbsentExcused));
        }
    }
}
