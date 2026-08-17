using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Activities;
using Sms.Domain.Attendance;
using Sms.Domain.Grades;

namespace Sms.Application.Activities
{
    /// <summary>
    /// doc/Modules/29 §8 Program catalog / Enrollment board / Trip
    /// console / Achievements center screens backing (screens deferred,
    /// the operations are core). Costed enrollment activation posts a
    /// real charge via E-303's IFeeAdmin (BR-ACT-007: costed programs
    /// follow full finance rules; free programs never touch Fees).
    /// Venue/timetable conflict surfacing (BR-ACT-001) and in-school
    /// attendance reconciliation (BR-ACT-003) are deferred — flagged in
    /// the domain entities' own doc comments.
    /// </summary>
    public interface IActivityAdmin
    {
        Task<ActivityType> DefineActivityTypeAsync(string nameAr, string nameEn, ActivityCategory category, CancellationToken cancellationToken = default);

        Task<ActivityProgram> DefineProgramAsync(
            int activityTypeId, int termId, string nameAr, string nameEn, int supervisorEmployeeId, int capacity, bool requiresConsent,
            int? venueRoomId = null, GenderPolicy? eligibilityGenderPolicy = null, int? eligibilityStageId = null,
            decimal? costAmount = null, int? feeCategoryId = null, DayOfWeek? dayOfWeek = null, TimeSpan? startTime = null,
            TimeSpan? endTime = null, CancellationToken cancellationToken = default);

        Task ApproveProgramAsync(int programId, CancellationToken cancellationToken = default);

        Task ActivateProgramAsync(int programId, CancellationToken cancellationToken = default);

        Task CloseProgramAsync(int programId, CancellationToken cancellationToken = default);

        /// <summary>BR-ACT-002: waitlists past capacity (BR-ADM-006 pattern); routes to ConsentPending when the program requires consent, else straight to Active (posting a charge first if costed).</summary>
        Task<ProgramEnrollment> RequestEnrollmentAsync(int programId, int studentId, int? payerId = null, CancellationToken cancellationToken = default);

        /// <summary>BR-ACT-005: records the consent and, if the enrollment was only pending on it, activates (posting a charge first if costed).</summary>
        Task<ActivityConsentRecord> GrantConsentAsync(
            int programEnrollmentId, string consentTextSnapshot, int grantedByUserId, int? payerId = null, CancellationToken cancellationToken = default);

        Task WithdrawEnrollmentAsync(int programEnrollmentId, string reason, CancellationToken cancellationToken = default);

        Task<ActivitySession> CreateSessionAsync(int programId, DateTime date, CancellationToken cancellationToken = default);

        Task<ActivityAttendance> CaptureAttendanceAsync(int activitySessionId, int programEnrollmentId, AttendanceStatus status, CancellationToken cancellationToken = default);

        Task<ActivityTrip> DefineTripAsync(int programId, string itineraryText, int staffRatioRequired, int? transportRouteId = null, CancellationToken cancellationToken = default);

        Task AssignTripStaffAsync(int activityTripId, int assignedStaffCount, CancellationToken cancellationToken = default);

        /// <summary>For external/chartered transport (no Route attached) — a Route-backed trip is already confirmed at definition time.</summary>
        Task ConfirmTransportAsync(int activityTripId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.TripNotReadyForDepartureException"/> (BR-ACT-004: ratio, consents, transport).</summary>
        Task ConfirmDepartureAsync(int activityTripId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.TripHeadcountMismatchException"/> (BR-TRN-005 sweep pattern).</summary>
        Task ConfirmReturnAsync(int activityTripId, int returnedHeadcount, CancellationToken cancellationToken = default);

        Task<CompetitionEvent> DefineCompetitionEventAsync(string nameAr, string nameEn, DateTime date, string? externalBodyRef = null, CancellationToken cancellationToken = default);

        Task<Achievement> RecordAchievementAsync(
            int studentId, string title, DateTime awardedAtUtc, int? programId = null, int? competitionEventId = null,
            int? certificateIssueId = null, CancellationToken cancellationToken = default);
    }
}
