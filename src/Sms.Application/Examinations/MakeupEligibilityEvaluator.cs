using Sms.Domain.Attendance;

namespace Sms.Application.Examinations
{
    /// <summary>Pure BR-EXM-008: excused/medical exam absences are system-derived makeup-eligible.</summary>
    public static class MakeupEligibilityEvaluator
    {
        public static bool IsSystemEligible(AttendanceStatus examAttendanceStatus)
            => examAttendanceStatus == AttendanceStatus.AbsentExcused || examAttendanceStatus == AttendanceStatus.MedicalLeave;
    }
}
