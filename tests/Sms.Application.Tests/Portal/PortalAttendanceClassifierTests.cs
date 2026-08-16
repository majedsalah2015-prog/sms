using Sms.Application.Portal;
using Sms.Domain.Attendance;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Portal
{
    public class PortalAttendanceClassifierTests
    {
        [Theory]
        [InlineData(AttendanceStatus.AbsentExcused, true)]
        [InlineData(AttendanceStatus.AbsentUnexcused, true)]
        [InlineData(AttendanceStatus.MedicalLeave, true)]
        [InlineData(AttendanceStatus.Present, false)]
        [InlineData(AttendanceStatus.Late, false)]
        [InlineData(AttendanceStatus.Permission, false)]
        [InlineData(AttendanceStatus.EarlyLeave, false)]
        [InlineData(AttendanceStatus.Exempted, false)]
        [BusinessRule("BR-ATD-009")]
        public void IsAbsent_covers_all_absence_flavors(AttendanceStatus status, bool expected)
        {
            Assert.Equal(expected, PortalAttendanceClassifier.IsAbsent(status));
        }

        [Theory]
        [InlineData(AttendanceStatus.Exempted, true)]
        [InlineData(AttendanceStatus.Present, false)]
        [BusinessRule("BR-ATD-009")]
        public void IsExempted_only_matches_exempted(AttendanceStatus status, bool expected)
        {
            Assert.Equal(expected, PortalAttendanceClassifier.IsExempted(status));
        }
    }
}
