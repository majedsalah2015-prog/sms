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
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.6 — the question bank. Standalone shape: each method
    /// saves itself.
    ///
    /// <para>
    /// <b>Reach is measured on the offering, not the section.</b> A bank belongs
    /// to a subject-year, and a teacher who holds one section of it authors for
    /// the subject rather than for that class — so the (offering, section) pairs
    /// BR-LRN-002 resolves are collapsed to their offerings here. That is a
    /// widening of nothing: a teacher with no placement on the offering at all
    /// still reaches none of it.
    /// </para>
    ///
    /// <para>
    /// <b>A revision is a row, never an edit.</b> BR-LRN-007's promise is that a
    /// past paper renders as it was answered, and the only way to keep that
    /// promise without a rule somebody has to remember is to leave the old row
    /// alone — with its own options and its own accepted answers, which are
    /// therefore copied into the new version rather than shared with it.
    /// </para>
    /// </summary>
    public class QuestionBankAdmin : IQuestionBankAdmin
    {
        private readonly AppDbContext _db;
        private readonly ICurrentUser _user;
        private readonly IHomeworkAdmin _homeworkAdmin;

        public QuestionBankAdmin(AppDbContext db, ICurrentUser user, IHomeworkAdmin homeworkAdmin)
        {
            _db = db;
            _user = user;
            _homeworkAdmin = homeworkAdmin;
        }

        public async Task<IReadOnlyList<int>> ReachableOfferingsAsync(
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            if (hasSchoolWideReach)
            {
                return await _db.CurriculumOfferings.AsNoTracking()
                    .Select(o => o.Id)
                    .ToListAsync(cancellationToken);
            }

            var reachable = await _homeworkAdmin.ReachableSectionsAsync(false, cancellationToken);

            return reachable.Select(r => r.CurriculumOfferingId).Distinct().ToList();
        }

        public async Task<IReadOnlyList<QuestionBank>> BanksAsync(
            int curriculumOfferingId,
            bool includeRetired = false,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            await GuardOfferingAsync(curriculumOfferingId, hasSchoolWideReach, cancellationToken);

            // Retired banks are read through the filter's back door on purpose:
            // §7 keeps versioned catalogs loadable, and a bank retired in March
            // still owns the questions on February's paper.
            var query = _db.QuestionBanks.IgnoreQueryFilters().AsNoTracking()
                .Where(b => b.SchoolId == _db.CurrentSchoolId
                    && b.CurriculumOfferingId == curriculumOfferingId);

            if (!includeRetired)
            {
                query = query.Where(b => b.IsActive);
            }

            var banks = await query.ToListAsync(cancellationToken);

            // BR-LRN-007's sharing, applied in memory because it turns on the
            // acting user rather than on the row: an author always sees their own
            // work, and everyone reaching the offering sees what was shared to it.
            return hasSchoolWideReach
                ? banks
                : banks
                    .Where(b => b.ShareScope != QuestionShareScope.AuthorOnly || b.CreatedByUserId == _user.UserId)
                    .ToList();
        }

        public async Task<QuestionBank> CreateBankAsync(
            int curriculumOfferingId,
            string nameAr,
            string nameEn,
            QuestionShareScope shareScope = QuestionShareScope.AuthorOnly,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            await GuardOfferingAsync(curriculumOfferingId, hasSchoolWideReach, cancellationToken);

            var offering = await _db.CurriculumOfferings.AsNoTracking()
                .SingleAsync(o => o.Id == curriculumOfferingId, cancellationToken);

            var bank = new QuestionBank
            {
                AcademicYearId = offering.AcademicYearId,
                CurriculumOfferingId = curriculumOfferingId,
                NameAr = nameAr.Trim(),
                NameEn = nameEn.Trim(),
                ShareScope = shareScope,
            };

            _db.QuestionBanks.Add(bank);
            await _db.SaveChangesAsync(cancellationToken);

            return bank;
        }

        public async Task<QuestionBank> UpdateBankAsync(
            int questionBankId,
            string nameAr,
            string nameEn,
            QuestionShareScope shareScope,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var bank = await LoadBankAsync(questionBankId, hasSchoolWideReach, cancellationToken);

            bank.NameAr = nameAr.Trim();
            bank.NameEn = nameEn.Trim();
            bank.ShareScope = shareScope;

            await _db.SaveChangesAsync(cancellationToken);

            return bank;
        }

        public async Task RetireBankAsync(
            int questionBankId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var bank = await LoadBankAsync(questionBankId, hasSchoolWideReach, cancellationToken);

            bank.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Question>> QuestionsAsync(
            int questionBankId,
            QuestionType? type = null,
            QuestionDifficulty? difficulty = null,
            bool includeDeprecated = false,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            await LoadBankAsync(questionBankId, hasSchoolWideReach, cancellationToken, track: false);

            var query = _db.Questions.AsNoTracking()
                .Where(q => q.QuestionBankId == questionBankId && q.IsCurrentVersion);

            if (!includeDeprecated)
            {
                query = query.Where(q => !q.IsDeprecated);
            }

            if (type is QuestionType t)
            {
                query = query.Where(q => q.Type == t);
            }

            if (difficulty is QuestionDifficulty d)
            {
                query = query.Where(q => q.Difficulty == d);
            }

            return await query.OrderByDescending(q => q.Id).ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Question>> VersionsAsync(
            int rootQuestionId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var any = await _db.Questions.AsNoTracking()
                .SingleOrDefaultAsync(q => q.RootQuestionId == rootQuestionId && q.Version == 1, cancellationToken);
            if (any == null)
            {
                return new List<Question>();
            }

            await LoadBankAsync(any.QuestionBankId, hasSchoolWideReach, cancellationToken, track: false);

            return await _db.Questions.AsNoTracking()
                .Where(q => q.RootQuestionId == rootQuestionId)
                .OrderBy(q => q.Version)
                .ToListAsync(cancellationToken);
        }

        public async Task<(IReadOnlyList<QuestionOption> Options, IReadOnlyList<QuestionAcceptedAnswer> AcceptedAnswers)> DetailAsync(
            int questionId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var question = await _db.Questions.AsNoTracking()
                .SingleAsync(q => q.Id == questionId, cancellationToken);

            await LoadBankAsync(question.QuestionBankId, hasSchoolWideReach, cancellationToken, track: false);

            var options = await _db.QuestionOptions.AsNoTracking()
                .Where(o => o.QuestionId == questionId)
                .OrderBy(o => o.DisplayOrder)
                .ToListAsync(cancellationToken);

            var answers = await _db.QuestionAcceptedAnswers.AsNoTracking()
                .Where(a => a.QuestionId == questionId)
                .OrderBy(a => a.Id)
                .ToListAsync(cancellationToken);

            return (options, answers);
        }

        public async Task<Question> AddQuestionAsync(
            int questionBankId,
            QuestionDraft draft,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var bank = await LoadBankAsync(questionBankId, hasSchoolWideReach, cancellationToken);

            if (!bank.IsActive)
            {
                throw new QuestionBankRetiredException(questionBankId);
            }

            GuardShape(draft);

            var question = NewVersionOf(draft, bank, rootQuestionId: 0, version: 1);

            _db.Questions.Add(question);
            await _db.SaveChangesAsync(cancellationToken);

            // Version 1 is its own root. Written after the insert because the id
            // is what it is naming, and a root that pointed at nothing would make
            // every later revision unfindable.
            question.RootQuestionId = question.Id;
            await WriteChildrenAsync(question, draft, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            return question;
        }

        public async Task<Question> ReviseQuestionAsync(
            int questionId,
            QuestionDraft draft,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var current = await _db.Questions.SingleAsync(q => q.Id == questionId, cancellationToken);
            var bank = await LoadBankAsync(current.QuestionBankId, hasSchoolWideReach, cancellationToken);

            if (current.IsDeprecated)
            {
                throw new QuestionDeprecatedException(questionId);
            }

            if (!current.IsCurrentVersion)
            {
                throw new QuestionNotCurrentVersionException(questionId, current.Version);
            }

            GuardShape(draft);

            var next = NewVersionOf(draft, bank, current.RootQuestionId, current.Version + 1);

            // The old row keeps its wording, its options and its accepted answers.
            // Nothing about it is touched except that it stops being the one a
            // future pick will find — which is exactly BR-LRN-007's promise.
            current.IsCurrentVersion = false;

            _db.Questions.Add(next);
            await _db.SaveChangesAsync(cancellationToken);

            await WriteChildrenAsync(next, draft, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            return next;
        }

        public async Task DeprecateQuestionAsync(
            int questionId,
            string reason,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A deprecation reason is required (BR-LRN-007).", nameof(reason));
            }

            var question = await _db.Questions.SingleAsync(q => q.Id == questionId, cancellationToken);
            await LoadBankAsync(question.QuestionBankId, hasSchoolWideReach, cancellationToken);

            question.IsDeprecated = true;
            question.DeprecatedReason = reason.Trim();

            await _db.SaveChangesAsync(cancellationToken);
        }

        // ---------------------------------------------------------------- helpers

        private static void GuardShape(QuestionDraft draft)
        {
            var refusal = QuestionTypeRules.Check(
                draft.Type,
                draft.Marks,
                draft.Options.Select(o => o.IsCorrect).ToList(),
                draft.AcceptedAnswers,
                draft.NumericTolerance);

            if (refusal != QuestionShapeRefusal.None)
            {
                throw new QuestionShapeException(refusal, draft.Type);
            }
        }

        private static Question NewVersionOf(QuestionDraft draft, QuestionBank bank, int rootQuestionId, int version)
            => new()
            {
                AcademicYearId = bank.AcademicYearId,
                QuestionBankId = bank.Id,
                RootQuestionId = rootQuestionId,
                Version = version,
                IsCurrentVersion = true,
                Type = draft.Type,
                StemAr = draft.StemAr.Trim(),
                StemEn = draft.StemEn.Trim(),
                Marks = draft.Marks,
                Difficulty = draft.Difficulty,
                LessonId = draft.LessonId,
                NumericTolerance = draft.NumericTolerance,
                ExplanationAr = string.IsNullOrWhiteSpace(draft.ExplanationAr) ? null : draft.ExplanationAr.Trim(),
                ExplanationEn = string.IsNullOrWhiteSpace(draft.ExplanationEn) ? null : draft.ExplanationEn.Trim(),
            };

        /// <summary>Options and accepted answers are copied per version, never shared — see the class remarks.</summary>
        private async Task WriteChildrenAsync(Question question, QuestionDraft draft, CancellationToken cancellationToken)
        {
            var order = 1;
            foreach (var option in draft.Options)
            {
                _db.QuestionOptions.Add(new QuestionOption
                {
                    AcademicYearId = question.AcademicYearId,
                    QuestionId = question.Id,
                    TextAr = option.TextAr.Trim(),
                    TextEn = option.TextEn.Trim(),
                    IsCorrect = option.IsCorrect,
                    DisplayOrder = order++,
                });
            }

            foreach (var answer in draft.AcceptedAnswers.Where(a => !string.IsNullOrWhiteSpace(a)))
            {
                _db.QuestionAcceptedAnswers.Add(new QuestionAcceptedAnswer
                {
                    AcademicYearId = question.AcademicYearId,
                    QuestionId = question.Id,
                    Text = answer.Trim(),
                });
            }

            await Task.CompletedTask;
        }

        private async Task<QuestionBank> LoadBankAsync(
            int questionBankId, bool hasSchoolWideReach, CancellationToken cancellationToken, bool track = true)
        {
            var query = _db.QuestionBanks.IgnoreQueryFilters();
            if (!track)
            {
                query = query.AsNoTracking();
            }

            var bank = await query.SingleAsync(
                b => b.Id == questionBankId && b.SchoolId == _db.CurrentSchoolId, cancellationToken);

            await GuardOfferingAsync(bank.CurriculumOfferingId, hasSchoolWideReach, cancellationToken);

            return bank;
        }

        private async Task GuardOfferingAsync(
            int curriculumOfferingId, bool hasSchoolWideReach, CancellationToken cancellationToken)
        {
            if (hasSchoolWideReach)
            {
                return;
            }

            var reachable = await ReachableOfferingsAsync(false, cancellationToken);

            if (!reachable.Contains(curriculumOfferingId))
            {
                throw new TeachingReachException(curriculumOfferingId);
            }
        }
    }
}
