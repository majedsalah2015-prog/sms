using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Attendance;

namespace Sms.Application.Attendance
{
    /// <summary>
    /// doc/Modules/14 §8 Section capture sheet / Gate console / Justification
    /// review queue / Correction screen backing (screens deferred, the
    /// operations are core). **Daily mode only** — Period mode needs
    /// Module 15's timetable sessions. Status corrections on AttendanceDay
    /// require the ambient IAuditContext.Reason (T1, same pattern as every
    /// other T1-tagged entity — set by the caller before invoking, the
    /// generic SmsDbContext pipeline enforces it).
    /// </summary>
    public interface IAttendanceAdmin
    {
        /// <summary>Throws <see cref="Common.Exceptions.DuplicateAttendanceRecordException"/> or <see cref="Common.Exceptions.NoSectionMembershipOnDateException"/>.</summary>
        Task<AttendanceDay> CaptureAsync(
            int enrollmentId, DateTime date, AttendanceStatus status, int capturedByUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-ATD-007: usable before or after closure; requires the ambient audit reason either way (T1).</summary>
        Task CorrectAsync(int attendanceDayId, AttendanceStatus newStatus, CancellationToken cancellationToken = default);

        /// <summary>BR-ATD-007: locks every AttendanceDay row captured for the given date (current tenant scope).</summary>
        Task<int> CloseDayAsync(DateTime date, CancellationToken cancellationToken = default);

        Task<Justification> SubmitJustificationAsync(
            int attendanceDayId, JustificationType type, DateTime submittedAtUtc, CancellationToken cancellationToken = default);

        /// <summary>BR-ATD-005: accepting flips the referenced AttendanceDay's status to Excused/Medical. Throws <see cref="Common.Exceptions.InvalidJustificationReviewException"/>.</summary>
        Task ReviewJustificationAsync(
            int justificationId, bool accept, int reviewedByUserId, DateTime reviewedAtUtc, string? rejectionReason = null,
            CancellationToken cancellationToken = default);

        Task<LeavePass> RequestLeavePassAsync(int enrollmentId, string reason, DateTime requestedAtUtc, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidLeavePassTransitionException"/>.</summary>
        Task ChangeLeavePassStatusAsync(int leavePassId, LeavePassStatus newStatus, DateTime whenUtc, CancellationToken cancellationToken = default);

        Task<GateEvent> RecordGateEventAsync(
            int enrollmentId, GateEventType eventType, DateTime eventTimeUtc, string? pickupPersonName = null,
            bool isAuthorizedPickupOverride = false, int? releasedByUserId = null, CancellationToken cancellationToken = default);
    }
}
