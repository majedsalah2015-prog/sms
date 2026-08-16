using Sms.Domain.Attendance;

namespace Sms.Application.Portal
{
    /// <summary>Pure: which AttendanceStatus values count as "absent" or "exempted" for BR-ATD-009's central %-computation, as seen from the portal summary.</summary>
    public static class PortalAttendanceClassifier
    {
        public static bool IsAbsent(AttendanceStatus status)
            => status == AttendanceStatus.AbsentExcused || status == AttendanceStatus.AbsentUnexcused || status == AttendanceStatus.MedicalLeave;

        public static bool IsExempted(AttendanceStatus status)
            => status == AttendanceStatus.Exempted;
    }
}
