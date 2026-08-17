using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Examinations;
using Sms.Domain.Attendance;
using Sms.Domain.Examinations;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Examinations
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class ExaminationAdmin : IExaminationAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;

        public ExaminationAdmin(AppDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<ExamType> DefineExamTypeAsync(
            string nameAr, string nameEn, bool isScheduled, bool isMakeupEligible, CancellationToken cancellationToken = default)
        {
            var type = new ExamType { NameAr = nameAr, NameEn = nameEn, IsScheduled = isScheduled, IsMakeupEligible = isMakeupEligible };
            _db.ExamTypes.Add(type);
            await _db.SaveChangesAsync(cancellationToken);
            return type;
        }

        public async Task<ExamRound> DefineRoundAsync(
            int academicYearId, int termId, string nameAr, string nameEn, CancellationToken cancellationToken = default)
        {
            var round = new ExamRound { AcademicYearId = academicYearId, TermId = termId, NameAr = nameAr, NameEn = nameEn };
            _db.ExamRounds.Add(round);
            await _db.SaveChangesAsync(cancellationToken);
            return round;
        }

        public async Task<Exam> ScheduleExamAsync(
            int examRoundId, int examTypeId, int curriculumOfferingId, int gradeYearProfileId, int blueprintComponentId,
            DateTime date, TimeSpan startTime, int durationMinutes, int maxExamsPerGradeYearPerDay = 1, CancellationToken cancellationToken = default)
        {
            var round = await _db.ExamRounds.SingleAsync(r => r.Id == examRoundId, cancellationToken);

            var component = await _db.BlueprintComponents.SingleAsync(c => c.Id == blueprintComponentId, cancellationToken);
            var blueprint = await _db.Blueprints.SingleAsync(b => b.Id == component.BlueprintId, cancellationToken);
            if (blueprint.CurriculumOfferingId != curriculumOfferingId || blueprint.TermId != round.TermId)
            {
                throw new ExamBlueprintMismatchException(blueprintComponentId);
            }

            var sameDayCount = await _db.Exams.CountAsync(
                e => e.GradeYearProfileId == gradeYearProfileId && e.Date == date.Date, cancellationToken);
            if (sameDayCount >= maxExamsPerGradeYearPerDay)
            {
                throw new ExamScheduleClashException(gradeYearProfileId, date);
            }

            var exam = new Exam
            {
                ExamRoundId = examRoundId, ExamTypeId = examTypeId, CurriculumOfferingId = curriculumOfferingId,
                GradeYearProfileId = gradeYearProfileId, BlueprintComponentId = blueprintComponentId,
                Date = date.Date, StartTime = startTime, DurationMinutes = durationMinutes,
            };
            _db.Exams.Add(exam);
            await _db.SaveChangesAsync(cancellationToken);
            return exam;
        }

        public async Task ValidateRoundAsync(int examRoundId, CancellationToken cancellationToken = default)
        {
            var round = await _db.ExamRounds.SingleAsync(r => r.Id == examRoundId, cancellationToken);
            if (!ExamRoundStatusTransitions.CanTransition(round.Status, ExamRoundStatus.Validated))
            {
                throw new InvalidExamRoundStatusTransitionException(round.Status, ExamRoundStatus.Validated);
            }

            round.Status = ExamRoundStatus.Validated;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task PublishRoundAsync(int examRoundId, int publishedByUserId, CancellationToken cancellationToken = default)
        {
            var round = await _db.ExamRounds.SingleAsync(r => r.Id == examRoundId, cancellationToken);
            if (!ExamRoundStatusTransitions.CanTransition(round.Status, ExamRoundStatus.Published))
            {
                throw new InvalidExamRoundStatusTransitionException(round.Status, ExamRoundStatus.Published);
            }

            round.Status = ExamRoundStatus.Published;
            round.PublishedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ExamSitting> CreateSittingAsync(int examId, int roomId, CancellationToken cancellationToken = default)
        {
            var sitting = new ExamSitting { ExamId = examId, RoomId = roomId };
            _db.ExamSittings.Add(sitting);
            await _db.SaveChangesAsync(cancellationToken);
            return sitting;
        }

        public async Task<ExamAttendance> SeatStudentAsync(int examSittingId, int enrollmentId, CancellationToken cancellationToken = default)
        {
            var sitting = await _db.ExamSittings.SingleAsync(s => s.Id == examSittingId, cancellationToken);
            var room = await _db.Rooms.SingleAsync(r => r.Id == sitting.RoomId, cancellationToken);
            var currentlySeated = await _db.ExamAttendances.CountAsync(a => a.ExamSittingId == examSittingId, cancellationToken);

            if (!SeatingCapacityEvaluator.HasCapacity(currentlySeated, room.ExamCapacity))
            {
                throw new SittingFullException(examSittingId);
            }

            var attendance = new ExamAttendance { ExamSittingId = examSittingId, EnrollmentId = enrollmentId, Status = AttendanceStatus.Present };
            _db.ExamAttendances.Add(attendance);
            await _db.SaveChangesAsync(cancellationToken);
            return attendance;
        }

        public async Task RecordExamAttendanceAsync(
            int examSittingId, int enrollmentId, AttendanceStatus status, bool unexcusedZeroPolicyEnabled = true,
            CancellationToken cancellationToken = default)
        {
            var attendance = await _db.ExamAttendances.SingleOrDefaultAsync(
                a => a.ExamSittingId == examSittingId && a.EnrollmentId == enrollmentId, cancellationToken);
            if (attendance == null)
            {
                throw new StudentNotSeatedException(examSittingId, enrollmentId);
            }

            attendance.Status = status;

            if (MakeupEligibilityEvaluator.IsSystemEligible(status))
            {
                var sitting = await _db.ExamSittings.SingleAsync(s => s.Id == examSittingId, cancellationToken);
                var alreadyEligible = await _db.MakeupEligibilities.AnyAsync(
                    m => m.ExamId == sitting.ExamId && m.EnrollmentId == enrollmentId, cancellationToken);
                if (!alreadyEligible)
                {
                    _db.MakeupEligibilities.Add(new MakeupEligibility { ExamId = sitting.ExamId, EnrollmentId = enrollmentId, IsSystemDerived = true });
                }
            }
            else if (ExamAbsenceMarkPolicy.ShouldZeroMark(status, unexcusedZeroPolicyEnabled))
            {
                var sitting = await _db.ExamSittings.SingleAsync(s => s.Id == examSittingId, cancellationToken);
                var exam = await _db.Exams.SingleAsync(e => e.Id == sitting.ExamId, cancellationToken);
                var markEntry = await _db.MarkEntries.SingleOrDefaultAsync(
                    e => e.BlueprintComponentId == exam.BlueprintComponentId && e.EnrollmentId == enrollmentId, cancellationToken);
                if (markEntry != null)
                {
                    markEntry.Score = 0m;
                    markEntry.IsAbsent = true;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ExamIncident> RecordIncidentAsync(
            int examSittingId, int enrollmentId, string category, string narrative, int recordedByUserId,
            CancellationToken cancellationToken = default)
        {
            var incident = new ExamIncident
            {
                ExamSittingId = examSittingId, EnrollmentId = enrollmentId, Category = category, Narrative = narrative,
                RecordedByUserId = recordedByUserId, RecordedAtUtc = _clock.UtcNow,
            };
            _db.ExamIncidents.Add(incident);
            await _db.SaveChangesAsync(cancellationToken);
            return incident;
        }

        public async Task<MakeupEligibility> ExtendMakeupEligibilityAsync(
            int examId, int enrollmentId, int approvedByUserId, CancellationToken cancellationToken = default)
        {
            var eligibility = await _db.MakeupEligibilities.SingleOrDefaultAsync(
                m => m.ExamId == examId && m.EnrollmentId == enrollmentId, cancellationToken);
            if (eligibility == null)
            {
                eligibility = new MakeupEligibility { ExamId = examId, EnrollmentId = enrollmentId };
                _db.MakeupEligibilities.Add(eligibility);
            }

            eligibility.ApprovedByUserId = approvedByUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return eligibility;
        }
    }
}
