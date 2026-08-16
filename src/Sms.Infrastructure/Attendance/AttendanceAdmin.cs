using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Common.Exceptions;
using Sms.Domain.Attendance;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Attendance
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class AttendanceAdmin : IAttendanceAdmin
    {
        private readonly AppDbContext _db;

        public AttendanceAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<AttendanceDay> CaptureAsync(
            int enrollmentId, DateTime date, AttendanceStatus status, int capturedByUserId, CancellationToken cancellationToken = default)
        {
            var duplicate = await _db.AttendanceDays.AnyAsync(
                a => a.EnrollmentId == enrollmentId && a.Date == date.Date, cancellationToken);
            if (duplicate)
            {
                throw new DuplicateAttendanceRecordException(enrollmentId, date);
            }

            var enrollment = await _db.Enrollments.SingleAsync(e => e.Id == enrollmentId, cancellationToken);

            var membership = await _db.SectionMemberships
                .Where(m => m.EnrollmentId == enrollmentId && m.EffectiveFromUtc <= date
                    && (m.EffectiveToUtc == null || m.EffectiveToUtc > date))
                .SingleOrDefaultAsync(cancellationToken);
            if (membership == null)
            {
                throw new NoSectionMembershipOnDateException(enrollmentId, date);
            }

            var attendanceDay = new AttendanceDay
            {
                AcademicYearId = enrollment.AcademicYearId,
                EnrollmentId = enrollmentId,
                SectionId = membership.SectionId,
                Date = date.Date,
                Status = status,
                CapturedByUserId = capturedByUserId,
            };
            _db.AttendanceDays.Add(attendanceDay);

            await _db.SaveChangesAsync(cancellationToken);
            return attendanceDay;
        }

        public async Task CorrectAsync(int attendanceDayId, AttendanceStatus newStatus, CancellationToken cancellationToken = default)
        {
            var attendanceDay = await _db.AttendanceDays.SingleAsync(a => a.Id == attendanceDayId, cancellationToken);
            attendanceDay.Status = newStatus;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> CloseDayAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var rows = await _db.AttendanceDays.Where(a => a.Date == date.Date && !a.IsLocked).ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                row.IsLocked = true;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return rows.Count;
        }

        public async Task<Justification> SubmitJustificationAsync(
            int attendanceDayId, JustificationType type, DateTime submittedAtUtc, CancellationToken cancellationToken = default)
        {
            var justification = new Justification
            {
                AttendanceDayId = attendanceDayId,
                Type = type,
                SubmittedAtUtc = submittedAtUtc,
                ReviewState = JustificationReviewState.Submitted,
            };
            _db.Justifications.Add(justification);

            await _db.SaveChangesAsync(cancellationToken);
            return justification;
        }

        public async Task ReviewJustificationAsync(
            int justificationId, bool accept, int reviewedByUserId, DateTime reviewedAtUtc, string? rejectionReason = null,
            CancellationToken cancellationToken = default)
        {
            var justification = await _db.Justifications.SingleAsync(j => j.Id == justificationId, cancellationToken);
            var newState = accept ? JustificationReviewState.Accepted : JustificationReviewState.Rejected;
            if (!JustificationTransitions.CanTransition(justification.ReviewState, newState))
            {
                throw new InvalidJustificationReviewException(justification.ReviewState, newState);
            }

            justification.ReviewState = newState;
            justification.ReviewedByUserId = reviewedByUserId;
            justification.ReviewedAtUtc = reviewedAtUtc;
            justification.RejectionReason = accept ? null : rejectionReason;

            if (accept)
            {
                var attendanceDay = await _db.AttendanceDays.SingleAsync(a => a.Id == justification.AttendanceDayId, cancellationToken);
                attendanceDay.Status = justification.Type == JustificationType.Medical
                    ? AttendanceStatus.MedicalLeave
                    : AttendanceStatus.AbsentExcused;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<LeavePass> RequestLeavePassAsync(int enrollmentId, string reason, DateTime requestedAtUtc, CancellationToken cancellationToken = default)
        {
            var leavePass = new LeavePass
            {
                EnrollmentId = enrollmentId,
                Reason = reason,
                RequestedAtUtc = requestedAtUtc,
                Status = LeavePassStatus.Requested,
            };
            _db.LeavePasses.Add(leavePass);

            await _db.SaveChangesAsync(cancellationToken);
            return leavePass;
        }

        public async Task ChangeLeavePassStatusAsync(int leavePassId, LeavePassStatus newStatus, DateTime whenUtc, CancellationToken cancellationToken = default)
        {
            var leavePass = await _db.LeavePasses.SingleAsync(l => l.Id == leavePassId, cancellationToken);
            if (!LeavePassTransitions.CanTransition(leavePass.Status, newStatus))
            {
                throw new InvalidLeavePassTransitionException(leavePass.Status, newStatus);
            }

            leavePass.Status = newStatus;
            switch (newStatus)
            {
                case LeavePassStatus.Released:
                    leavePass.ReleasedAtUtc = whenUtc;
                    break;
                case LeavePassStatus.Returned:
                    leavePass.ReturnedAtUtc = whenUtc;
                    break;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<GateEvent> RecordGateEventAsync(
            int enrollmentId, GateEventType eventType, DateTime eventTimeUtc, string? pickupPersonName = null,
            bool isAuthorizedPickupOverride = false, int? releasedByUserId = null, CancellationToken cancellationToken = default)
        {
            var gateEvent = new GateEvent
            {
                EnrollmentId = enrollmentId,
                EventType = eventType,
                EventTimeUtc = eventTimeUtc,
                PickupPersonName = pickupPersonName,
                IsAuthorizedPickupOverride = isAuthorizedPickupOverride,
                ReleasedByUserId = releasedByUserId,
            };
            _db.GateEvents.Add(gateEvent);

            await _db.SaveChangesAsync(cancellationToken);
            return gateEvent;
        }
    }
}
