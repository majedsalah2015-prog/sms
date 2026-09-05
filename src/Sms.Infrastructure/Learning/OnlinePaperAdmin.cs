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
    /// doc/Modules/37 §8.7 — the paper builder. Standalone shape: each method
    /// saves itself.
    ///
    /// <para>
    /// Reach is delegated to <see cref="IQuestionBankAdmin"/> rather than
    /// recomputed: a paper belongs to a bank and a bank to an offering, so asking
    /// the bank is asking the same question once instead of twice.
    /// </para>
    ///
    /// <para>
    /// <b>Every mark total is summed in memory.</b> <c>SumAsync</c> over a decimal
    /// compiles and then throws at run time on Sqlite, which is what the whole
    /// test suite runs on — so the rows are materialised and added here. That is a
    /// deliberate shape, not an oversight, and a paper is tens of rows.
    /// </para>
    /// </summary>
    public class OnlinePaperAdmin : IOnlinePaperAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly ICurrentUser _user;
        private readonly IQuestionBankAdmin _banks;

        public OnlinePaperAdmin(AppDbContext db, IClock clock, ICurrentUser user, IQuestionBankAdmin banks)
        {
            _db = db;
            _clock = clock;
            _user = user;
            _banks = banks;
        }

        public async Task<IReadOnlyList<OnlinePaper>> PapersAsync(
            int questionBankId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            await GuardBankAsync(questionBankId, hasSchoolWideReach, cancellationToken);

            return await _db.OnlinePapers.AsNoTracking()
                .Where(p => p.QuestionBankId == questionBankId)
                .OrderByDescending(p => p.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<OnlinePaper> CreateAsync(
            int questionBankId,
            int blueprintComponentId,
            string titleAr,
            string titleEn,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var bank = await GuardBankAsync(questionBankId, hasSchoolWideReach, cancellationToken);

            if (!bank.IsActive)
            {
                throw new QuestionBankRetiredException(questionBankId);
            }

            // The component must exist before the paper names it: BR-LRN-008
            // measures against its MaxScore on every later read, and a paper
            // pointing at nothing would have no target at all.
            _ = await _db.BlueprintComponents.AsNoTracking()
                .SingleAsync(c => c.Id == blueprintComponentId, cancellationToken);

            var paper = new OnlinePaper
            {
                AcademicYearId = bank.AcademicYearId,
                QuestionBankId = questionBankId,
                BlueprintComponentId = blueprintComponentId,
                TitleAr = titleAr.Trim(),
                TitleEn = titleEn.Trim(),
                Status = OnlinePaperStatus.Draft,
            };

            _db.OnlinePapers.Add(paper);
            await _db.SaveChangesAsync(cancellationToken);

            return paper;
        }

        public async Task<IReadOnlyList<(PaperItem Item, Question Question)>> ItemsAsync(
            int onlinePaperId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            await LoadPaperAsync(onlinePaperId, hasSchoolWideReach, cancellationToken, track: false);

            var rows = await (
                from i in _db.PaperItems.AsNoTracking()
                join q in _db.Questions.AsNoTracking() on i.QuestionId equals q.Id
                where i.OnlinePaperId == onlinePaperId
                orderby i.DisplayOrder
                select new { Item = i, Question = q })
                .ToListAsync(cancellationToken);

            return rows.Select(r => (r.Item, r.Question)).ToList();
        }

        public async Task<PaperReconciliation> ReconciliationAsync(
            int onlinePaperId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var paper = await LoadPaperAsync(onlinePaperId, hasSchoolWideReach, cancellationToken, track: false);

            return await ReconcileAsync(paper, cancellationToken);
        }

        public async Task<PaperItem> AddItemAsync(
            int onlinePaperId,
            int questionId,
            decimal? marks = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var paper = await LoadPaperAsync(onlinePaperId, hasSchoolWideReach, cancellationToken);
            GuardEditable(paper);

            var question = await _db.Questions.AsNoTracking()
                .SingleAsync(q => q.Id == questionId, cancellationToken);

            if (question.QuestionBankId != paper.QuestionBankId)
            {
                throw new QuestionNotInBankException(questionId, paper.QuestionBankId);
            }

            var item = NewItem(paper, question, marks, await NextOrderAsync(onlinePaperId, cancellationToken));

            _db.PaperItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);

            return item;
        }

        public async Task RemoveItemAsync(
            int paperItemId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var item = await _db.PaperItems.SingleAsync(i => i.Id == paperItemId, cancellationToken);
            var paper = await LoadPaperAsync(item.OnlinePaperId, hasSchoolWideReach, cancellationToken);
            GuardEditable(paper);

            // A draft paper's items are working notes, not a record of anything a
            // student saw, so this is the one place in the module where a row is
            // physically removed rather than ended. BR-GLB-005 governs records; a
            // question taken off a paper nobody has sat is not one.
            _db.PaperItems.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> GenerateAsync(
            int onlinePaperId,
            int count,
            int? lessonId = null,
            QuestionDifficulty? difficulty = null,
            QuestionType? type = null,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return 0;
            }

            var paper = await LoadPaperAsync(onlinePaperId, hasSchoolWideReach, cancellationToken);
            GuardEditable(paper);

            var already = await _db.PaperItems.AsNoTracking()
                .Where(i => i.OnlinePaperId == onlinePaperId)
                .Select(i => i.QuestionId)
                .ToListAsync(cancellationToken);

            var query = _db.Questions.AsNoTracking()
                .Where(q => q.QuestionBankId == paper.QuestionBankId
                    && q.IsCurrentVersion
                    && !q.IsDeprecated
                    && !already.Contains(q.Id));

            if (lessonId is int lesson)
            {
                query = query.Where(q => q.LessonId == lesson);
            }

            if (difficulty is QuestionDifficulty d)
            {
                query = query.Where(q => q.Difficulty == d);
            }

            if (type is QuestionType t)
            {
                query = query.Where(q => q.Type == t);
            }

            var picked = await query.OrderBy(q => q.Id).Take(count).ToListAsync(cancellationToken);

            var order = await NextOrderAsync(onlinePaperId, cancellationToken);

            foreach (var question in picked)
            {
                _db.PaperItems.Add(NewItem(paper, question, null, order++));
            }

            await _db.SaveChangesAsync(cancellationToken);

            return picked.Count;
        }

        public async Task SubmitForApprovalAsync(
            int onlinePaperId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
            => await MoveAsync(onlinePaperId, OnlinePaperStatus.Draft, OnlinePaperStatus.PendingApproval, hasSchoolWideReach, cancellationToken);

        public async Task ApproveAsync(
            int onlinePaperId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var paper = await MoveAsync(
                onlinePaperId, OnlinePaperStatus.PendingApproval, OnlinePaperStatus.Approved, hasSchoolWideReach, cancellationToken);

            paper.ApprovedByUserId = _user.UserId;
            paper.ApprovedAtUtc = _clock.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task RejectAsync(
            int onlinePaperId,
            string reason,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A rejection reason is required (doc/Modules/37 §4).", nameof(reason));
            }

            var paper = await LoadPaperAsync(onlinePaperId, hasSchoolWideReach, cancellationToken);

            if (!OnlinePaperStatusTransitions.CanTransition(paper.Status, OnlinePaperStatus.Draft))
            {
                throw new OnlinePaperTransitionException(onlinePaperId, paper.Status, OnlinePaperStatus.Draft);
            }

            // The reason rides the T2 audit trail rather than a column: a rejection
            // is a moment in the paper's history, and the paper's own fields
            // describe what it is now, which is a draft again.
            paper.Status = OnlinePaperStatus.Draft;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task WithdrawAsync(
            int onlinePaperId,
            string reason,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A withdrawal reason is required (BR-LRN-016).", nameof(reason));
            }

            var paper = await LoadPaperAsync(onlinePaperId, hasSchoolWideReach, cancellationToken);

            if (!OnlinePaperStatusTransitions.CanTransition(paper.Status, OnlinePaperStatus.Withdrawn))
            {
                throw new OnlinePaperTransitionException(onlinePaperId, paper.Status, OnlinePaperStatus.Withdrawn);
            }

            paper.Status = OnlinePaperStatus.Withdrawn;
            paper.WithdrawnReason = reason.Trim();
            paper.WithdrawnAtUtc = _clock.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// The one place BR-LRN-008 is applied, so submit and approve cannot give
        /// different answers about the same paper.
        /// </summary>
        private async Task<OnlinePaper> MoveAsync(
            int onlinePaperId,
            OnlinePaperStatus from,
            OnlinePaperStatus to,
            bool hasSchoolWideReach,
            CancellationToken cancellationToken)
        {
            var paper = await LoadPaperAsync(onlinePaperId, hasSchoolWideReach, cancellationToken);

            // The lifecycle answer comes first and separately, because it deserves
            // its own sentence: someone submitting a withdrawn paper needs to be
            // told it was withdrawn, not that "the move is unavailable". The gate's
            // own status check stays as the second line of defence.
            if (!OnlinePaperStatusTransitions.CanTransition(paper.Status, to))
            {
                throw new OnlinePaperTransitionException(onlinePaperId, paper.Status, to);
            }

            var reconciliation = await ReconcileAsync(paper, cancellationToken);

            var refusal = PaperReconciliationGate.Check(
                paper.Status,
                from,
                reconciliation.ItemCount,
                reconciliation.TotalMarks,
                reconciliation.ComponentMaxScore,
                reconciliation.WithdrawnQuestionCount);

            if (refusal != PaperRefusal.None)
            {
                throw new PaperRefusedException(
                    onlinePaperId, refusal, reconciliation.TotalMarks,
                    reconciliation.ComponentMaxScore, reconciliation.WithdrawnQuestionCount);
            }

            paper.Status = to;
            await _db.SaveChangesAsync(cancellationToken);

            return paper;
        }

        private async Task<PaperReconciliation> ReconcileAsync(OnlinePaper paper, CancellationToken cancellationToken)
        {
            var component = await _db.BlueprintComponents.AsNoTracking()
                .SingleAsync(c => c.Id == paper.BlueprintComponentId, cancellationToken);

            // Materialised, then added in memory: SumAsync over a decimal throws
            // on Sqlite at run time (see the class remarks).
            var rows = await (
                from i in _db.PaperItems.AsNoTracking()
                join q in _db.Questions.AsNoTracking() on i.QuestionId equals q.Id
                where i.OnlinePaperId == paper.Id
                select new { i.Marks, q.IsDeprecated })
                .ToListAsync(cancellationToken);

            return new PaperReconciliation
            {
                ItemCount = rows.Count,
                TotalMarks = rows.Count == 0 ? 0m : rows.Select(r => r.Marks).Sum(),
                ComponentMaxScore = component.MaxScore,
                ComponentName = component.NameEn,
                WithdrawnQuestionCount = rows.Count(r => r.IsDeprecated),
            };
        }

        private static PaperItem NewItem(OnlinePaper paper, Question question, decimal? marks, int order) => new()
        {
            AcademicYearId = paper.AcademicYearId,
            OnlinePaperId = paper.Id,
            // The version's own id, never its root: this is what freezes the
            // question against a later revision (BR-LRN-007).
            QuestionId = question.Id,
            DisplayOrder = order,
            Marks = marks ?? question.Marks,
        };

        private async Task<int> NextOrderAsync(int onlinePaperId, CancellationToken cancellationToken)
        {
            var orders = await _db.PaperItems.AsNoTracking()
                .Where(i => i.OnlinePaperId == onlinePaperId)
                .Select(i => i.DisplayOrder)
                .ToListAsync(cancellationToken);

            return orders.Count == 0 ? 1 : orders.Max() + 1;
        }

        private static void GuardEditable(OnlinePaper paper)
        {
            if (!OnlinePaperStatusTransitions.IsEditable(paper.Status))
            {
                throw new PaperNotEditableException(paper.Id, paper.Status);
            }
        }

        private async Task<OnlinePaper> LoadPaperAsync(
            int onlinePaperId, bool hasSchoolWideReach, CancellationToken cancellationToken, bool track = true)
        {
            var query = _db.OnlinePapers.AsQueryable();
            if (!track)
            {
                query = query.AsNoTracking();
            }

            var paper = await query.SingleAsync(p => p.Id == onlinePaperId, cancellationToken);

            await GuardBankAsync(paper.QuestionBankId, hasSchoolWideReach, cancellationToken);

            return paper;
        }

        /// <summary>
        /// Reach comes from the bank, through the port that already owns it —
        /// asking twice is how two copies of one rule start disagreeing.
        /// </summary>
        private async Task<QuestionBank> GuardBankAsync(
            int questionBankId, bool hasSchoolWideReach, CancellationToken cancellationToken)
        {
            var bank = await _db.QuestionBanks.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(b => b.Id == questionBankId && b.SchoolId == _db.CurrentSchoolId, cancellationToken);

            var reachable = await _banks.ReachableOfferingsAsync(hasSchoolWideReach, cancellationToken);

            if (!hasSchoolWideReach && !reachable.Contains(bank.CurriculumOfferingId))
            {
                throw new TeachingReachException(bank.CurriculumOfferingId);
            }

            return bank;
        }
    }
}
