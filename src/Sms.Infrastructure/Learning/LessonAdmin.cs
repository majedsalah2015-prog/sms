using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Learning;
using Sms.Domain.Learning;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.1-2 — the lesson planner and its resource library.
    /// Standalone shape: each method saves itself.
    ///
    /// BR-LRN-002 reach is resolved here and enforced on every write, including
    /// the resource operations, so a caller cannot reach a lesson through its
    /// attachments that it could not reach directly.
    /// </summary>
    public class LessonAdmin : ILessonAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly ICurrentUser _user;

        public LessonAdmin(AppDbContext db, IClock clock, ICurrentUser user)
        {
            _db = db;
            _clock = clock;
            _user = user;
        }

        public async Task<IReadOnlyList<int>> ReachableOfferingIdsAsync(bool hasSchoolWideReach = false, CancellationToken cancellationToken = default)
        {
            if (hasSchoolWideReach)
            {
                return await _db.CurriculumOfferings.Select(o => o.Id).ToListAsync(cancellationToken);
            }

            var placements = await LoadPlacementsAsync(cancellationToken);
            var departmentOfferings = await LoadDepartmentOfferingsAsync(cancellationToken);

            return placements.Select(p => p.CurriculumOfferingId)
                .Concat(departmentOfferings)
                .Distinct()
                .ToList();
        }

        public async Task<Lesson> CreateAsync(
            int curriculumOfferingId,
            int weekNumber,
            string titleAr,
            string titleEn,
            string? objectivesAr = null,
            string? objectivesEn = null,
            int? sessionId = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            await GuardReachAsync(curriculumOfferingId, hasSchoolWideReach, cancellationToken);
            await GuardSessionTeachesOfferingAsync(sessionId, curriculumOfferingId, cancellationToken);

            // BR-LRN-001: the year comes from the anchor, so a lesson can never
            // sit in a different year from the offering it teaches.
            var yearId = await _db.CurriculumOfferings
                .Where(o => o.Id == curriculumOfferingId)
                .Select(o => o.AcademicYearId)
                .SingleAsync(cancellationToken);

            var lesson = new Lesson
            {
                AcademicYearId = yearId,
                CurriculumOfferingId = curriculumOfferingId,
                SessionId = sessionId,
                WeekNumber = weekNumber,
                TitleAr = titleAr,
                TitleEn = titleEn,
                ObjectivesAr = objectivesAr,
                ObjectivesEn = objectivesEn,
                Status = LessonStatus.Draft,
            };

            _db.Lessons.Add(lesson);
            await _db.SaveChangesAsync(cancellationToken);
            return lesson;
        }

        public async Task<Lesson> UpdateAsync(
            int lessonId,
            int weekNumber,
            string titleAr,
            string titleEn,
            string? objectivesAr = null,
            string? objectivesEn = null,
            int? sessionId = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var lesson = await _db.Lessons.SingleAsync(l => l.Id == lessonId, cancellationToken);
            await GuardReachAsync(lesson.CurriculumOfferingId, hasSchoolWideReach, cancellationToken);

            // BR-LRN-016: retired content is readable history, not an editable draft.
            if (lesson.Status == LessonStatus.Retired)
            {
                throw new LessonRetiredException(lessonId);
            }

            await GuardSessionTeachesOfferingAsync(sessionId, lesson.CurriculumOfferingId, cancellationToken);

            lesson.WeekNumber = weekNumber;
            lesson.TitleAr = titleAr;
            lesson.TitleEn = titleEn;
            lesson.ObjectivesAr = objectivesAr;
            lesson.ObjectivesEn = objectivesEn;
            lesson.SessionId = sessionId;

            await _db.SaveChangesAsync(cancellationToken);
            return lesson;
        }

        public async Task PublishAsync(int lessonId, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default)
        {
            var lesson = await _db.Lessons.SingleAsync(l => l.Id == lessonId, cancellationToken);
            await GuardReachAsync(lesson.CurriculumOfferingId, hasSchoolWideReach, cancellationToken);

            if (!LessonStatusTransitions.CanTransition(lesson.Status, LessonStatus.Published))
            {
                throw new LessonTransitionException(lessonId, lesson.Status, LessonStatus.Published);
            }

            lesson.Status = LessonStatus.Published;
            lesson.PublishedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task RetireAsync(int lessonId, string reason, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A retirement reason is required (BR-LRN-016).", nameof(reason));
            }

            var lesson = await _db.Lessons.SingleAsync(l => l.Id == lessonId, cancellationToken);
            await GuardReachAsync(lesson.CurriculumOfferingId, hasSchoolWideReach, cancellationToken);

            if (!LessonStatusTransitions.CanTransition(lesson.Status, LessonStatus.Retired))
            {
                throw new LessonTransitionException(lessonId, lesson.Status, LessonStatus.Retired);
            }

            lesson.Status = LessonStatus.Retired;
            lesson.RetiredReason = reason;
            lesson.RetiredAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<LessonResource> AttachResourceAsync(
            int lessonId,
            int attachmentId,
            string titleAr,
            string titleEn,
            int displayOrder = 0,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var lesson = await _db.Lessons.SingleAsync(l => l.Id == lessonId, cancellationToken);
            await GuardReachAsync(lesson.CurriculumOfferingId, hasSchoolWideReach, cancellationToken);

            if (lesson.Status == LessonStatus.Retired)
            {
                throw new LessonRetiredException(lessonId);
            }

            var resource = new LessonResource
            {
                AcademicYearId = lesson.AcademicYearId,
                LessonId = lessonId,
                AttachmentId = attachmentId,
                TitleAr = titleAr,
                TitleEn = titleEn,
                DisplayOrder = displayOrder,
                IsActive = true,
            };

            _db.LessonResources.Add(resource);
            await _db.SaveChangesAsync(cancellationToken);
            return resource;
        }

        public async Task WithdrawResourceAsync(int lessonResourceId, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default)
        {
            var resource = await _db.LessonResources.SingleAsync(r => r.Id == lessonResourceId, cancellationToken);
            var offeringId = await _db.Lessons
                .Where(l => l.Id == resource.LessonId)
                .Select(l => l.CurriculumOfferingId)
                .SingleAsync(cancellationToken);

            await GuardReachAsync(offeringId, hasSchoolWideReach, cancellationToken);

            // BR-GLB-005: withdrawn, never deleted.
            resource.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>BR-LRN-002. Throws <see cref="TeachingReachException"/> when the acting user reaches neither the offering's sections nor its department.</summary>
        private async Task GuardReachAsync(int curriculumOfferingId, bool hasSchoolWideReach, CancellationToken cancellationToken)
        {
            if (hasSchoolWideReach)
            {
                return;
            }

            var placements = await LoadPlacementsAsync(cancellationToken);
            var departmentOfferings = await LoadDepartmentOfferingsAsync(cancellationToken);

            if (!TeachingReachEvaluator.CanAuthorContent(placements, departmentOfferings, false, curriculumOfferingId))
            {
                throw new TeachingReachException(curriculumOfferingId);
            }
        }

        /// <summary>BR-LRN-002: reach is measured in the published timetable version only — a draft timetable grants nothing.</summary>
        private async Task<List<PlacementReach>> LoadPlacementsAsync(CancellationToken cancellationToken)
        {
            var teacherProfileId = await (
                from e in _db.Employees
                join t in _db.TeacherProfiles on e.Id equals t.EmployeeId
                where e.UserAccountId == _user.UserId
                select (int?)t.Id).FirstOrDefaultAsync(cancellationToken);

            if (teacherProfileId is null)
            {
                return new List<PlacementReach>();
            }

            var pairs = await (
                from p in _db.Placements
                join v in _db.TimetableVersions on p.TimetableVersionId equals v.Id
                where p.TeacherProfileId == teacherProfileId
                      && v.Status == TimetableVersionStatus.Published
                select new { p.CurriculumOfferingId, p.SectionId })
                .ToListAsync(cancellationToken);

            return pairs.Select(p => new PlacementReach(p.CurriculumOfferingId, p.SectionId)).ToList();
        }

        /// <summary>
        /// BR-LRN-002 Head-of-Department reach. The department list stays behind
        /// the soft-active filter deliberately: a deactivated department grants
        /// its former head no reach, and the query returns an empty list rather
        /// than failing, so no IgnoreQueryFilters lookup is needed here.
        /// </summary>
        private async Task<List<int>> LoadDepartmentOfferingsAsync(CancellationToken cancellationToken)
        {
            return await (
                from o in _db.CurriculumOfferings
                join s in _db.Subjects on o.SubjectId equals s.Id
                join d in _db.Departments on s.DepartmentId equals d.Id
                where d.HeadTeacherUserId == _user.UserId
                select o.Id).ToListAsync(cancellationToken);
        }

        /// <summary>BR-LRN-001: a bound lesson must name a session that actually teaches this offering.</summary>
        private async Task GuardSessionTeachesOfferingAsync(int? sessionId, int curriculumOfferingId, CancellationToken cancellationToken)
        {
            if (sessionId is null)
            {
                return;
            }

            var teaches = await (
                from s in _db.Sessions
                join p in _db.Placements on s.PlacementId equals p.Id
                where s.Id == sessionId && p.CurriculumOfferingId == curriculumOfferingId
                select s.Id).AnyAsync(cancellationToken);

            if (!teaches)
            {
                throw new LessonSessionMismatchException(sessionId.Value, curriculumOfferingId);
            }
        }
    }
}
