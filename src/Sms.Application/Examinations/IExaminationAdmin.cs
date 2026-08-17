using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Attendance;
using Sms.Domain.Examinations;

namespace Sms.Application.Examinations
{
    /// <summary>
    /// doc/Modules/16 §8 Schedule builder / Seating allocator / Sitting
    /// console / Makeup manager screens backing (screens deferred, the
    /// operations are core). Marks capture itself reuses E-302's
    /// IGradingAdmin.CreateMarksheetAsync/EnterMarkAsync/ChangeMarksheetStatusAsync
    /// directly (doc's own "single marks store" mandate) — this interface
    /// owns exam scheduling, seating, attendance, incidents, and makeup
    /// eligibility only. Invigilation duty rosters (BR-EXM-005) are
    /// deferred — lower-priority than the scheduling/marks path, same
    /// "defer a named sub-feature" precedent as training records in E-203.
    /// </summary>
    public interface IExaminationAdmin
    {
        Task<ExamType> DefineExamTypeAsync(
            string nameAr, string nameEn, bool isScheduled, bool isMakeupEligible, CancellationToken cancellationToken = default);

        Task<ExamRound> DefineRoundAsync(int academicYearId, int termId, string nameAr, string nameEn, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.ExamBlueprintMismatchException"/> (BR-EXM-002) or <see cref="Common.Exceptions.ExamScheduleClashException"/> (BR-EXM-003).</summary>
        Task<Exam> ScheduleExamAsync(
            int examRoundId, int examTypeId, int curriculumOfferingId, int gradeYearProfileId, int blueprintComponentId,
            DateTime date, TimeSpan startTime, int durationMinutes, int maxExamsPerGradeYearPerDay = 1, CancellationToken cancellationToken = default);

        /// <summary>Draft -> Validated (BR-EXM §4).</summary>
        Task ValidateRoundAsync(int examRoundId, CancellationToken cancellationToken = default);

        /// <summary>Validated -> Published (BR-EXM §4, P2 VP not enforced here).</summary>
        Task PublishRoundAsync(int examRoundId, int publishedByUserId, CancellationToken cancellationToken = default);

        Task<ExamSitting> CreateSittingAsync(int examId, int roomId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.SittingFullException"/> (BR-EXM-004/BR-ROM-002).</summary>
        Task<ExamAttendance> SeatStudentAsync(int examSittingId, int enrollmentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-EXM-006: excused/medical marks the student system-eligible for
        /// makeup (BR-EXM-008). Unexcused zeroes the exam's MarkEntry per
        /// <see cref="ExamAbsenceMarkPolicy"/> when unexcusedZeroPolicyEnabled
        /// is set — a real cross-module write into E-302's MarkEntry, not
        /// just a status flag. Throws <see cref="Common.Exceptions.StudentNotSeatedException"/>.
        /// </summary>
        Task RecordExamAttendanceAsync(
            int examSittingId, int enrollmentId, AttendanceStatus status, bool unexcusedZeroPolicyEnabled = true,
            CancellationToken cancellationToken = default);

        Task<ExamIncident> RecordIncidentAsync(
            int examSittingId, int enrollmentId, string category, string narrative, int recordedByUserId,
            CancellationToken cancellationToken = default);

        /// <summary>BR-EXM-008: manual extension beyond the system-derived list (T1, reason expected via the ambient audit context).</summary>
        Task<MakeupEligibility> ExtendMakeupEligibilityAsync(int examId, int enrollmentId, int approvedByUserId, CancellationToken cancellationToken = default);
    }
}
