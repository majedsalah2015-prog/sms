using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Calendar;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Learning;
using Sms.Application.Setup;
using Sms.Domain.Calendar;
using Sms.Domain.Learning;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.3 — the homework desk. Standalone shape: each method
    /// saves itself.
    ///
    /// BR-LRN-002 reach is resolved here and enforced on every write, and
    /// BR-LRN-004's gate is applied inside <see cref="IssueAsync"/> rather than
    /// left to the screen, so neither can be skipped by a second caller.
    /// </summary>
    public class HomeworkAdmin : IHomeworkAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly ICurrentUser _user;
        private readonly ISystemSetupAdmin _setup;

        public HomeworkAdmin(AppDbContext db, IClock clock, ICurrentUser user, ISystemSetupAdmin setup)
        {
            _db = db;
            _clock = clock;
            _user = user;
            _setup = setup;
        }

        public async Task<IReadOnlyList<PlacementReach>> ReachableSectionsAsync(
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            if (hasSchoolWideReach)
            {
                // Every taught (offering, section) pair in the published timetable.
                return await LoadAllPublishedPairsAsync(cancellationToken);
            }

            var placements = await LoadPlacementsAsync(cancellationToken);
            var departmentOfferings = await LoadDepartmentOfferingsAsync(cancellationToken);

            if (departmentOfferings.Count == 0)
            {
                return placements;
            }

            // BR-LRN-002: a head of department reaches every section of their
            // department's offerings, not only the ones they personally teach.
            var departmental = (await LoadAllPublishedPairsAsync(cancellationToken))
                .Where(p => departmentOfferings.Contains(p.CurriculumOfferingId));

            return placements
                .Concat(departmental)
                .GroupBy(p => (p.CurriculumOfferingId, p.SectionId))
                .Select(g => g.First())
                .ToList();
        }

        public async Task<Homework> CreateAsync(
            int curriculumOfferingId,
            int sectionId,
            string titleAr,
            string titleEn,
            DateTime dueDate,
            string? instructionsAr = null,
            string? instructionsEn = null,
            decimal? maxMarks = null,
            int? blueprintComponentId = null,
            LatenessPolicy latenessPolicy = LatenessPolicy.AcceptWithoutPenalty,
            decimal? latePenaltyPercent = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            await GuardReachAsync(curriculumOfferingId, sectionId, hasSchoolWideReach, cancellationToken);

            // BR-LRN-001: the year comes from the anchor, so homework can never
            // sit in a different year from the offering it is set against.
            var yearId = await _db.CurriculumOfferings
                .Where(o => o.Id == curriculumOfferingId)
                .Select(o => o.AcademicYearId)
                .SingleAsync(cancellationToken);

            var homework = new Homework
            {
                AcademicYearId = yearId,
                CurriculumOfferingId = curriculumOfferingId,
                SectionId = sectionId,
                TitleAr = titleAr,
                TitleEn = titleEn,
                InstructionsAr = instructionsAr,
                InstructionsEn = instructionsEn,
                DueDate = dueDate.Date,
                MaxMarks = maxMarks,
                BlueprintComponentId = blueprintComponentId,
                LatenessPolicy = latenessPolicy,
                LatePenaltyPercent = latenessPolicy == LatenessPolicy.AcceptWithPenalty ? latePenaltyPercent : null,
                Status = HomeworkStatus.Draft,
            };

            _db.Homeworks.Add(homework);
            await _db.SaveChangesAsync(cancellationToken);
            return homework;
        }

        public async Task<Homework> UpdateAsync(
            int homeworkId,
            string titleAr,
            string titleEn,
            DateTime dueDate,
            string? instructionsAr = null,
            string? instructionsEn = null,
            decimal? maxMarks = null,
            int? blueprintComponentId = null,
            LatenessPolicy latenessPolicy = LatenessPolicy.AcceptWithoutPenalty,
            decimal? latePenaltyPercent = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var homework = await _db.Homeworks.SingleAsync(h => h.Id == homeworkId, cancellationToken);
            await GuardReachAsync(homework.CurriculumOfferingId, homework.SectionId, hasSchoolWideReach, cancellationToken);

            // Released work belongs to Module 17 now; withdrawn work is history.
            if (homework.Status is HomeworkStatus.Released or HomeworkStatus.Withdrawn)
            {
                throw new HomeworkTransitionException(homeworkId, homework.Status, homework.Status);
            }

            homework.TitleAr = titleAr;
            homework.TitleEn = titleEn;
            homework.InstructionsAr = instructionsAr;
            homework.InstructionsEn = instructionsEn;
            homework.DueDate = dueDate.Date;
            homework.MaxMarks = maxMarks;
            homework.BlueprintComponentId = blueprintComponentId;
            homework.LatenessPolicy = latenessPolicy;
            homework.LatePenaltyPercent = latenessPolicy == LatenessPolicy.AcceptWithPenalty ? latePenaltyPercent : null;

            await _db.SaveChangesAsync(cancellationToken);
            return homework;
        }

        public async Task IssueAsync(int homeworkId, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default)
        {
            var homework = await _db.Homeworks.SingleAsync(h => h.Id == homeworkId, cancellationToken);
            await GuardReachAsync(homework.CurriculumOfferingId, homework.SectionId, hasSchoolWideReach, cancellationToken);

            if (!HomeworkStatusTransitions.CanTransition(homework.Status, HomeworkStatus.Issued))
            {
                throw new HomeworkTransitionException(homeworkId, homework.Status, HomeworkStatus.Issued);
            }

            var year = await _db.AcademicYears
                .Where(y => y.Id == homework.AcademicYearId)
                .Select(y => new { y.StartDate, y.EndDate })
                .SingleAsync(cancellationToken);

            var isWorkingDay = await BuildWorkingDayPredicateAsync(homework.AcademicYearId, cancellationToken);

            var refusal = HomeworkIssueGate.Check(
                homework.MaxMarks,
                homework.BlueprintComponentId,
                homework.DueDate,
                year.StartDate,
                year.EndDate,
                isWorkingDay);

            if (refusal != HomeworkIssueRefusal.None)
            {
                throw new HomeworkIssueRefusedException(homeworkId, refusal);
            }

            homework.Status = HomeworkStatus.Issued;
            homework.IssuedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task WithdrawAsync(int homeworkId, string reason, bool hasSchoolWideReach = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A withdrawal reason is required (BR-LRN-016).", nameof(reason));
            }

            var homework = await _db.Homeworks.SingleAsync(h => h.Id == homeworkId, cancellationToken);
            await GuardReachAsync(homework.CurriculumOfferingId, homework.SectionId, hasSchoolWideReach, cancellationToken);

            if (!HomeworkStatusTransitions.CanTransition(homework.Status, HomeworkStatus.Withdrawn))
            {
                throw new HomeworkTransitionException(homeworkId, homework.Status, HomeworkStatus.Withdrawn);
            }

            // doc/Modules/37 §9: past the due date, work already handed in cannot
            // be made to have never been asked for.
            var submissions = await CountSubmissionsAsync(homeworkId, cancellationToken);
            if (submissions > 0 && homework.DueDate.Date < _clock.UtcNow.Date)
            {
                throw new HomeworkWithdrawalBlockedException(homeworkId, submissions);
            }

            homework.Status = HomeworkStatus.Withdrawn;
            homework.WithdrawnReason = reason;
            homework.WithdrawnAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// doc/Modules/37 §9's submission count.
        ///
        /// <para>
        /// <b>Deliberately zero in this slice.</b> <c>HomeworkSubmission</c> is
        /// §8.4's entity and does not exist yet, so no submission can exist to
        /// block a withdrawal. The guard above is written and its exception is
        /// tested, so the slice that adds submissions changes this one method and
        /// the rule starts holding — rather than having to notice, months later,
        /// that a withdrawal path was never guarded at all.
        /// </para>
        /// </summary>
        private Task<int> CountSubmissionsAsync(int homeworkId, CancellationToken cancellationToken)
            => Task.FromResult(0);

        /// <summary>BR-GLB-052: the school calendar decides which days are working days, not the day of the week alone.</summary>
        private async Task<Func<DateTime, bool>> BuildWorkingDayPredicateAsync(int academicYearId, CancellationToken cancellationToken)
        {
            var workingDaysSetting = await _setup.GetSettingAsync(SettingKeys.WorkingDays, academicYearId, cancellationToken);
            var weekend = new HashSet<DayOfWeek>(
                string.IsNullOrWhiteSpace(workingDaysSetting)
                    ? Array.Empty<DayOfWeek>()
                    : WorkingWeek.WeekendDays(workingDaysSetting));

            var overrides = await _db.CalendarDays
                .Where(d => d.AcademicYearId == academicYearId)
                .ToDictionaryAsync(d => d.Date.Date, d => d.DayType, cancellationToken);

            return date => CalendarDayResolver.Resolve(date, weekend, overrides) == DayType.Working;
        }

        /// <summary>BR-LRN-002. Throws <see cref="TeachingReachException"/> unless the acting user holds this (offering, section) pair or heads the offering's department.</summary>
        private async Task GuardReachAsync(int curriculumOfferingId, int sectionId, bool hasSchoolWideReach, CancellationToken cancellationToken)
        {
            if (hasSchoolWideReach)
            {
                return;
            }

            var placements = await LoadPlacementsAsync(cancellationToken);
            var departmentOfferings = await LoadDepartmentOfferingsAsync(cancellationToken);

            if (!TeachingReachEvaluator.CanIssueToSection(placements, departmentOfferings, false, curriculumOfferingId, sectionId))
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

        /// <summary>Every (offering, section) pair the published timetable actually teaches.</summary>
        private async Task<List<PlacementReach>> LoadAllPublishedPairsAsync(CancellationToken cancellationToken)
        {
            var pairs = await (
                from p in _db.Placements
                join v in _db.TimetableVersions on p.TimetableVersionId equals v.Id
                where v.Status == TimetableVersionStatus.Published
                select new { p.CurriculumOfferingId, p.SectionId })
                .Distinct()
                .ToListAsync(cancellationToken);

            return pairs.Select(p => new PlacementReach(p.CurriculumOfferingId, p.SectionId)).ToList();
        }

        /// <summary>
        /// BR-LRN-002 Head-of-Department reach. The department list stays behind
        /// the soft-active filter deliberately: a deactivated department grants
        /// its former head no reach.
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
    }
}
