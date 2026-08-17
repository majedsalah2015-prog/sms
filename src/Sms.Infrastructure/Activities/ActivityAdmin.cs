using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Activities;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Domain.Activities;
using Sms.Domain.Attendance;
using Sms.Domain.Grades;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Activities
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class ActivityAdmin : IActivityAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IFeeAdmin _feeAdmin;

        public ActivityAdmin(AppDbContext db, IClock clock, IFeeAdmin feeAdmin)
        {
            _db = db;
            _clock = clock;
            _feeAdmin = feeAdmin;
        }

        public async Task<ActivityType> DefineActivityTypeAsync(string nameAr, string nameEn, ActivityCategory category, CancellationToken cancellationToken = default)
        {
            var type = new ActivityType { NameAr = nameAr, NameEn = nameEn, Category = category };
            _db.ActivityTypes.Add(type);
            await _db.SaveChangesAsync(cancellationToken);
            return type;
        }

        public async Task<ActivityProgram> DefineProgramAsync(
            int activityTypeId, int termId, string nameAr, string nameEn, int supervisorEmployeeId, int capacity, bool requiresConsent,
            int? venueRoomId = null, GenderPolicy? eligibilityGenderPolicy = null, int? eligibilityStageId = null,
            decimal? costAmount = null, int? feeCategoryId = null, DayOfWeek? dayOfWeek = null, TimeSpan? startTime = null,
            TimeSpan? endTime = null, CancellationToken cancellationToken = default)
        {
            var term = await _db.Terms.SingleAsync(t => t.Id == termId, cancellationToken);

            var program = new ActivityProgram
            {
                AcademicYearId = term.AcademicYearId,
                TermId = termId,
                ActivityTypeId = activityTypeId,
                NameAr = nameAr,
                NameEn = nameEn,
                SupervisorEmployeeId = supervisorEmployeeId,
                VenueRoomId = venueRoomId,
                Capacity = capacity,
                EligibilityGenderPolicy = eligibilityGenderPolicy,
                EligibilityStageId = eligibilityStageId,
                CostAmount = costAmount,
                FeeCategoryId = feeCategoryId,
                RequiresConsent = requiresConsent,
                DayOfWeek = dayOfWeek,
                StartTime = startTime,
                EndTime = endTime,
            };
            _db.ActivityPrograms.Add(program);
            await _db.SaveChangesAsync(cancellationToken);
            return program;
        }

        private async Task ChangeProgramStatusAsync(int programId, ProgramStatus to, CancellationToken cancellationToken)
        {
            var program = await _db.ActivityPrograms.SingleAsync(p => p.Id == programId, cancellationToken);
            if (!ProgramStatusTransitions.CanTransition(program.Status, to))
            {
                throw new InvalidProgramStatusTransitionException(program.Status, to);
            }

            program.Status = to;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public Task ApproveProgramAsync(int programId, CancellationToken cancellationToken = default)
            => ChangeProgramStatusAsync(programId, ProgramStatus.Approved, cancellationToken);

        public Task ActivateProgramAsync(int programId, CancellationToken cancellationToken = default)
            => ChangeProgramStatusAsync(programId, ProgramStatus.Active, cancellationToken);

        public Task CloseProgramAsync(int programId, CancellationToken cancellationToken = default)
            => ChangeProgramStatusAsync(programId, ProgramStatus.Closed, cancellationToken);

        public async Task<ProgramEnrollment> RequestEnrollmentAsync(
            int programId, int studentId, int? payerId = null, CancellationToken cancellationToken = default)
        {
            var program = await _db.ActivityPrograms.SingleAsync(p => p.Id == programId, cancellationToken);
            var activeCount = await _db.ProgramEnrollments.CountAsync(
                e => e.ProgramId == programId && e.Status == ProgramEnrollmentStatus.Active, cancellationToken);
            var hasCapacity = ProgramCapacityEvaluator.HasCapacity(activeCount, program.Capacity);

            var enrollment = new ProgramEnrollment { ProgramId = programId, StudentId = studentId, RequestedAtUtc = _clock.UtcNow };
            _db.ProgramEnrollments.Add(enrollment);
            await _db.SaveChangesAsync(cancellationToken);

            if (!hasCapacity)
            {
                await TransitionEnrollmentAsync(enrollment, ProgramEnrollmentStatus.Waitlisted, cancellationToken);
            }
            else if (program.RequiresConsent)
            {
                await TransitionEnrollmentAsync(enrollment, ProgramEnrollmentStatus.ConsentPending, cancellationToken);
            }
            else
            {
                await ActivateEnrollmentAsync(enrollment, program, payerId, cancellationToken);
            }

            return enrollment;
        }

        private async Task TransitionEnrollmentAsync(ProgramEnrollment enrollment, ProgramEnrollmentStatus to, CancellationToken cancellationToken)
        {
            if (!ProgramEnrollmentStatusTransitions.CanTransition(enrollment.Status, to))
            {
                throw new InvalidProgramEnrollmentStatusTransitionException(enrollment.Status, to);
            }

            enrollment.Status = to;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task ActivateEnrollmentAsync(ProgramEnrollment enrollment, ActivityProgram program, int? payerId, CancellationToken cancellationToken)
        {
            if (!ProgramEnrollmentStatusTransitions.CanTransition(enrollment.Status, ProgramEnrollmentStatus.Active))
            {
                throw new InvalidProgramEnrollmentStatusTransitionException(enrollment.Status, ProgramEnrollmentStatus.Active);
            }

            if (program.CostAmount.HasValue)
            {
                if (payerId == null || program.FeeCategoryId == null)
                {
                    throw new InvalidOperationException("Costed program activation requires a payerId and the program's FeeCategoryId (BR-ACT-007).");
                }

                var charge = await _feeAdmin.PostManualChargeAsync(
                    enrollment.StudentId, payerId.Value, program.FeeCategoryId.Value, program.CostAmount.Value, cancellationToken);
                enrollment.ChargeId = charge.Id;
            }

            enrollment.Status = ProgramEnrollmentStatus.Active;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ActivityConsentRecord> GrantConsentAsync(
            int programEnrollmentId, string consentTextSnapshot, int grantedByUserId, int? payerId = null, CancellationToken cancellationToken = default)
        {
            var enrollment = await _db.ProgramEnrollments.SingleAsync(e => e.Id == programEnrollmentId, cancellationToken);

            var record = new ActivityConsentRecord
            {
                ProgramEnrollmentId = programEnrollmentId, ConsentTextSnapshot = consentTextSnapshot,
                GrantedByUserId = grantedByUserId, GrantedAtUtc = _clock.UtcNow,
            };
            _db.ActivityConsentRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);

            if (enrollment.Status == ProgramEnrollmentStatus.ConsentPending)
            {
                var program = await _db.ActivityPrograms.SingleAsync(p => p.Id == enrollment.ProgramId, cancellationToken);
                var activeCount = await _db.ProgramEnrollments.CountAsync(
                    e => e.ProgramId == enrollment.ProgramId && e.Status == ProgramEnrollmentStatus.Active, cancellationToken);

                if (ProgramCapacityEvaluator.HasCapacity(activeCount, program.Capacity))
                {
                    await ActivateEnrollmentAsync(enrollment, program, payerId, cancellationToken);
                }
                else
                {
                    await TransitionEnrollmentAsync(enrollment, ProgramEnrollmentStatus.Waitlisted, cancellationToken);
                }
            }

            return record;
        }

        public async Task WithdrawEnrollmentAsync(int programEnrollmentId, string reason, CancellationToken cancellationToken = default)
        {
            var enrollment = await _db.ProgramEnrollments.SingleAsync(e => e.Id == programEnrollmentId, cancellationToken);
            if (!ProgramEnrollmentStatusTransitions.CanTransition(enrollment.Status, ProgramEnrollmentStatus.Withdrawn))
            {
                throw new InvalidProgramEnrollmentStatusTransitionException(enrollment.Status, ProgramEnrollmentStatus.Withdrawn);
            }

            enrollment.Status = ProgramEnrollmentStatus.Withdrawn;
            enrollment.WithdrawalReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ActivitySession> CreateSessionAsync(int programId, DateTime date, CancellationToken cancellationToken = default)
        {
            var session = new ActivitySession { ProgramId = programId, Date = date.Date };
            _db.ActivitySessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);
            return session;
        }

        public async Task<ActivityAttendance> CaptureAttendanceAsync(
            int activitySessionId, int programEnrollmentId, AttendanceStatus status, CancellationToken cancellationToken = default)
        {
            var attendance = await _db.ActivityAttendances.SingleOrDefaultAsync(
                a => a.ActivitySessionId == activitySessionId && a.ProgramEnrollmentId == programEnrollmentId, cancellationToken);
            if (attendance == null)
            {
                attendance = new ActivityAttendance { ActivitySessionId = activitySessionId, ProgramEnrollmentId = programEnrollmentId };
                _db.ActivityAttendances.Add(attendance);
            }

            attendance.Status = status;
            await _db.SaveChangesAsync(cancellationToken);
            return attendance;
        }

        public async Task<ActivityTrip> DefineTripAsync(
            int programId, string itineraryText, int staffRatioRequired, int? transportRouteId = null, CancellationToken cancellationToken = default)
        {
            var trip = new ActivityTrip
            {
                ProgramId = programId, ItineraryText = itineraryText, StaffRatioRequired = staffRatioRequired,
                TransportRouteId = transportRouteId, TransportConfirmed = transportRouteId.HasValue,
            };
            _db.ActivityTrips.Add(trip);
            await _db.SaveChangesAsync(cancellationToken);
            return trip;
        }

        public async Task AssignTripStaffAsync(int activityTripId, int assignedStaffCount, CancellationToken cancellationToken = default)
        {
            var trip = await _db.ActivityTrips.SingleAsync(t => t.Id == activityTripId, cancellationToken);
            trip.AssignedStaffCount = assignedStaffCount;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ConfirmTransportAsync(int activityTripId, CancellationToken cancellationToken = default)
        {
            var trip = await _db.ActivityTrips.SingleAsync(t => t.Id == activityTripId, cancellationToken);
            trip.TransportConfirmed = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<int[]> GetActiveEnrollmentIdsAsync(int programId, CancellationToken cancellationToken)
            => await _db.ProgramEnrollments
                .Where(e => e.ProgramId == programId && e.Status == ProgramEnrollmentStatus.Active)
                .Select(e => e.Id)
                .ToArrayAsync(cancellationToken);

        public async Task ConfirmDepartureAsync(int activityTripId, CancellationToken cancellationToken = default)
        {
            var trip = await _db.ActivityTrips.SingleAsync(t => t.Id == activityTripId, cancellationToken);
            var activeEnrollmentIds = await GetActiveEnrollmentIdsAsync(trip.ProgramId, cancellationToken);

            var ratioSatisfied = TripStaffRatioEvaluator.IsSatisfied(activeEnrollmentIds.Length, trip.AssignedStaffCount, trip.StaffRatioRequired);

            var consentedIds = await _db.ActivityConsentRecords
                .Where(c => activeEnrollmentIds.Contains(c.ProgramEnrollmentId))
                .Select(c => c.ProgramEnrollmentId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var allConsentsCurrent = activeEnrollmentIds.All(id => consentedIds.Contains(id));

            if (!TripDepartureChecklistEvaluator.CanDepart(ratioSatisfied, allConsentsCurrent, trip.TransportConfirmed))
            {
                throw new TripNotReadyForDepartureException(activityTripId);
            }

            trip.DepartureChecklistComplete = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ConfirmReturnAsync(int activityTripId, int returnedHeadcount, CancellationToken cancellationToken = default)
        {
            var trip = await _db.ActivityTrips.SingleAsync(t => t.Id == activityTripId, cancellationToken);
            var activeEnrollmentIds = await GetActiveEnrollmentIdsAsync(trip.ProgramId, cancellationToken);

            if (!TripDepartureChecklistEvaluator.HeadcountMatches(activeEnrollmentIds.Length, returnedHeadcount))
            {
                throw new TripHeadcountMismatchException(activeEnrollmentIds.Length, returnedHeadcount);
            }

            trip.ReturnHeadcountConfirmed = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<CompetitionEvent> DefineCompetitionEventAsync(
            string nameAr, string nameEn, DateTime date, string? externalBodyRef = null, CancellationToken cancellationToken = default)
        {
            var competitionEvent = new CompetitionEvent { NameAr = nameAr, NameEn = nameEn, Date = date, ExternalBodyRef = externalBodyRef };
            _db.CompetitionEvents.Add(competitionEvent);
            await _db.SaveChangesAsync(cancellationToken);
            return competitionEvent;
        }

        public async Task<Achievement> RecordAchievementAsync(
            int studentId, string title, DateTime awardedAtUtc, int? programId = null, int? competitionEventId = null,
            int? certificateIssueId = null, CancellationToken cancellationToken = default)
        {
            var achievement = new Achievement
            {
                StudentId = studentId, Title = title, AwardedAtUtc = awardedAtUtc, ProgramId = programId,
                CompetitionEventId = competitionEventId, CertificateIssueId = certificateIssueId,
            };
            _db.Achievements.Add(achievement);
            await _db.SaveChangesAsync(cancellationToken);
            return achievement;
        }
    }
}
