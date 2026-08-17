using Sms.Domain.Attendance;

namespace Sms.Application.Examinations
{
    /// <summary>Pure BR-EXM-006/doc §14 open question #1: unexcused exam absence defaults to a zero mark with makeup denied (the doc's own proposed default) — configurable, never auto-applied without this explicit policy flag.</summary>
    public static class ExamAbsenceMarkPolicy
    {
        public static bool ShouldZeroMark(AttendanceStatus examAttendanceStatus, bool unexcusedZeroPolicyEnabled = true)
            => examAttendanceStatus == AttendanceStatus.AbsentUnexcused && unexcusedZeroPolicyEnabled;
    }
}
