using System;
using Sms.Domain.Attendance;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-ATD-003: one attendance record per enrollment per day.</summary>
    public class DuplicateAttendanceRecordException : InvalidOperationException
    {
        public DuplicateAttendanceRecordException(int enrollmentId, DateTime date)
            : base($"Attendance for enrollment {enrollmentId} on {date:yyyy-MM-dd} is already captured (BR-ATD-003).")
        {
        }
    }

    /// <summary>BR-ATD-003: the enrollment has no section-membership row covering the capture date.</summary>
    public class NoSectionMembershipOnDateException : InvalidOperationException
    {
        public NoSectionMembershipOnDateException(int enrollmentId, DateTime date)
            : base($"Enrollment {enrollmentId} has no section membership as of {date:yyyy-MM-dd} (BR-ATD-003).")
        {
        }
    }

    /// <summary>BR-ATD-005: the requested justification review state pair isn't legal.</summary>
    public class InvalidJustificationReviewException : InvalidOperationException
    {
        public InvalidJustificationReviewException(JustificationReviewState from, JustificationReviewState to)
            : base($"Justification review state cannot move from '{from}' to '{to}' (BR-ATD-005).")
        {
        }
    }

    /// <summary>BR-ATD-006: the requested leave-pass status pair isn't legal.</summary>
    public class InvalidLeavePassTransitionException : InvalidOperationException
    {
        public InvalidLeavePassTransitionException(LeavePassStatus from, LeavePassStatus to)
            : base($"Leave pass cannot move from '{from}' to '{to}' (BR-ATD-006).")
        {
        }
    }
}
