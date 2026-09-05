using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Learning;
using Sms.Application.Security;
using Sms.Domain.Learning;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/37 §8.7 — the paper builder.
    ///
    /// <para>
    /// <b>The Approve verb is the screen.</b> §6 gives Create and Edit to the
    /// teacher and Approve to the head of department, and the two sit on the same
    /// page: the author builds, the meter says whether it reconciles, and the head
    /// of department signs. Splitting them into two screens would have hidden the
    /// numbers from the person whose signature depends on them.
    /// </para>
    ///
    /// <para>
    /// BR-LRN-008's refusal is raised by the engine and translated at this
    /// boundary, where both totals are turned into a sentence. The rule requires
    /// the refusal to name them, and only the boundary knows the reader's
    /// language.
    /// </para>
    ///
    /// <para>
    /// The same DEVIATION as §8.1-8.6: <c>hasSchoolWideReach</c> is always false
    /// (BR-GLB-071). Deny by default.
    /// </para>
    /// </summary>
    public partial class LearningController
    {
        // ---------------------------------------------------------------- the papers of a bank

        [HttpGet("question-banks/{id:int}/papers")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.View)]
        public async Task<IActionResult> Papers(
            int id, [FromServices] IOnlinePaperAdmin papers)
        {
            var m = await BuildPapersAsync(id, papers);
            return m == null ? NotFound() : View(m);
        }

        [HttpPost("question-banks/{id:int}/papers/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.Create)]
        public async Task<IActionResult> CreatePaper(
            int id, int blueprintComponentId, string? titleAr, string? titleEn,
            [FromServices] IOnlinePaperAdmin papers)
        {
            if (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn))
            {
                TempData["Error"] = T("A paper needs both an Arabic and an English title.", "الورقة تحتاج عنواناً عربياً وآخر إنجليزياً.");
                return RedirectToAction(nameof(Papers), new { id });
            }

            try
            {
                var paper = await papers.CreateAsync(
                    id, blueprintComponentId, titleAr, titleEn, cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = T("Paper created. Add questions until it matches the component.",
                    "أُنشئت الورقة. أضف الأسئلة حتى تطابق المكوّن.");
                return RedirectToAction(nameof(Paper), new { id = paper.Id });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Papers), new { id });
        }

        // ---------------------------------------------------------------- one paper

        [HttpGet("papers/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.View)]
        public async Task<IActionResult> Paper(int id, [FromServices] IOnlinePaperAdmin papers)
        {
            var m = await BuildPaperAsync(id, papers);
            return m == null ? NotFound() : View(m);
        }

        [HttpPost("papers/{id:int}/items/add")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.Edit)]
        public async Task<IActionResult> AddPaperItem(
            int id, int questionId, string? marks, [FromServices] IOnlinePaperAdmin papers)
        {
            decimal? marksValue = null;
            if (!string.IsNullOrWhiteSpace(marks))
            {
                // Invariant, like every decimal this product posts.
                if (!decimal.TryParse(marks.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    TempData["Error"] = T("The mark must be a number.", "الدرجة يجب أن تكون رقماً.");
                    return RedirectToAction(nameof(Paper), new { id });
                }

                marksValue = parsed;
            }

            try
            {
                await papers.AddItemAsync(id, questionId, marksValue, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Question added.", "أُضيف السؤال.");
            }
            catch (DbUpdateException)
            {
                // The unique index over (paper, question) fired: the same question
                // twice would ask a student for it twice and mark them twice.
                TempData["Error"] = T("That question is already on this paper.", "هذا السؤال موجود على الورقة بالفعل.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Paper), new { id });
        }

        [HttpPost("papers/{id:int}/items/{itemId:int}/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.Edit)]
        public async Task<IActionResult> RemovePaperItem(int id, int itemId, [FromServices] IOnlinePaperAdmin papers)
        {
            try
            {
                await papers.RemoveItemAsync(itemId, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Question removed.", "حُذف السؤال.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Paper), new { id });
        }

        /// <summary>§8.7's generation rule: by topic, difficulty and type.</summary>
        [HttpPost("papers/{id:int}/generate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.Edit)]
        public async Task<IActionResult> GeneratePaper(
            int id, int count, int? lessonId, QuestionDifficulty? difficulty, QuestionType? type,
            [FromServices] IOnlinePaperAdmin papers)
        {
            try
            {
                var added = await papers.GenerateAsync(
                    id, count, lessonId, difficulty, type, cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = added == 0
                    // Saying so matters: silently adding nothing would leave an
                    // author believing the paper had been built.
                    ? T("No question in this bank matched — nothing was added.",
                        "لا سؤال في هذا البنك يطابق — ولم يُضف شيء.")
                    : added < count
                        ? T($"{added} question(s) added — the bank had no more that matched.",
                            $"أُضيف {added} سؤالاً — ولم يعد في البنك ما يطابق.")
                        : T($"{added} question(s) added.", $"أُضيف {added} سؤالاً.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Paper), new { id });
        }

        [HttpPost("papers/{id:int}/submit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.Edit)]
        public async Task<IActionResult> SubmitPaper(int id, [FromServices] IOnlinePaperAdmin papers)
        {
            try
            {
                await papers.SubmitForApprovalAsync(id, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Sent to the head of department.", "أُرسلت إلى رئيس القسم.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Paper), new { id });
        }

        /// <summary>§4 P2 — the head of department's signature. The Approve verb is granted to nobody else.</summary>
        [HttpPost("papers/{id:int}/approve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.Approve)]
        public async Task<IActionResult> ApprovePaper(int id, [FromServices] IOnlinePaperAdmin papers)
        {
            try
            {
                await papers.ApproveAsync(id, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Paper approved. It can now be scheduled to a sitting.",
                    "اعتُمدت الورقة. ويمكن الآن جدولتها لجلسة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Paper), new { id });
        }

        [HttpPost("papers/{id:int}/reject")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.Approve)]
        public async Task<IActionResult> RejectPaper(int id, string? reason, [FromServices] IOnlinePaperAdmin papers)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("Say what needs changing — the author has only this to go on.",
                    "اذكر ما ينبغي تغييره — فليس لدى كاتبها سواه.");
                return RedirectToAction(nameof(Paper), new { id });
            }

            try
            {
                await papers.RejectAsync(id, reason, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Handed back to the author as a draft.", "أُعيدت إلى كاتبها مسوّدةً.");
            }
            catch (ArgumentException)
            {
                TempData["Error"] = T("Say what needs changing.", "اذكر ما ينبغي تغييره.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Paper), new { id });
        }

        [HttpPost("papers/{id:int}/withdraw")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Papers, ActionVerb.Deactivate)]
        public async Task<IActionResult> WithdrawPaper(int id, string? reason, [FromServices] IOnlinePaperAdmin papers)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("Say why the paper is being withdrawn.", "اذكر سبب سحب الورقة.");
                return RedirectToAction(nameof(Paper), new { id });
            }

            try
            {
                await papers.WithdrawAsync(id, reason, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Paper withdrawn.", "سُحبت الورقة.");
            }
            catch (ArgumentException)
            {
                TempData["Error"] = T("Say why the paper is being withdrawn.", "اذكر سبب سحب الورقة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Paper), new { id });
        }

        // ---------------------------------------------------------------- shared

        private async Task<PapersViewModel?> BuildPapersAsync(int bankId, IOnlinePaperAdmin papers)
        {
            QuestionBank bank;
            IReadOnlyList<OnlinePaper> rows;
            try
            {
                bank = await _db.QuestionBanks.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(b => b.Id == bankId && b.SchoolId == _db.CurrentSchoolId, HttpContext.RequestAborted);

                rows = await papers.PapersAsync(bankId, cancellationToken: HttpContext.RequestAborted);
            }
            catch (TeachingReachException)
            {
                // BR-SEC-010: a bank this user does not reach simply is not there.
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            var withMeters = new List<PaperRow>();
            foreach (var paper in rows)
            {
                var reconciliation = await papers.ReconciliationAsync(paper.Id, cancellationToken: HttpContext.RequestAborted);
                withMeters.Add(new PaperRow(paper, IsArabic ? paper.TitleAr : paper.TitleEn, reconciliation));
            }

            // BR-LRN-008: only this offering's own blueprint components — a paper
            // cannot fill another subject's.
            var components = await (
                from c in _db.BlueprintComponents.AsNoTracking()
                join b in _db.Blueprints.AsNoTracking() on c.BlueprintId equals b.Id
                where b.CurriculumOfferingId == bank.CurriculumOfferingId
                select new PaperComponentOption(c.Id, IsArabic ? c.NameAr : c.NameEn, c.MaxScore))
                .ToListAsync(HttpContext.RequestAborted);

            return new PapersViewModel
            {
                Bank = bank,
                BankName = IsArabic ? bank.NameAr : bank.NameEn,
                Papers = withMeters,
                Components = components,
            };
        }

        private async Task<PaperViewModel?> BuildPaperAsync(int paperId, IOnlinePaperAdmin papers)
        {
            OnlinePaper paper;
            PaperReconciliation reconciliation;
            IReadOnlyList<(PaperItem Item, Question Question)> items;
            try
            {
                paper = await _db.OnlinePapers.AsNoTracking()
                    .SingleAsync(p => p.Id == paperId, HttpContext.RequestAborted);

                reconciliation = await papers.ReconciliationAsync(paperId, cancellationToken: HttpContext.RequestAborted);
                items = await papers.ItemsAsync(paperId, cancellationToken: HttpContext.RequestAborted);
            }
            catch (TeachingReachException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            var bank = await _db.QuestionBanks.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(b => b.Id == paper.QuestionBankId, HttpContext.RequestAborted);

            var onPaper = items.Select(i => i.Question.Id).ToHashSet();

            var candidates = await _db.Questions.AsNoTracking()
                .Where(q => q.QuestionBankId == paper.QuestionBankId && q.IsCurrentVersion && !q.IsDeprecated)
                .OrderBy(q => q.Id)
                .ToListAsync(HttpContext.RequestAborted);

            var lessons = await _db.Lessons.IgnoreQueryFilters().AsNoTracking()
                .Where(l => l.CurriculumOfferingId == bank.CurriculumOfferingId && l.SchoolId == _db.CurrentSchoolId)
                .OrderBy(l => l.WeekNumber)
                .Select(l => new ValueTuple<int, string>(l.Id, IsArabic ? l.TitleAr : l.TitleEn))
                .ToListAsync(HttpContext.RequestAborted);

            return new PaperViewModel
            {
                Paper = paper,
                Title = IsArabic ? paper.TitleAr : paper.TitleEn,
                BankName = IsArabic ? bank.NameAr : bank.NameEn,
                Reconciliation = reconciliation,
                Items = items,
                Candidates = candidates
                    .Select(q => new PaperCandidate(q, IsArabic ? q.StemAr : q.StemEn, onPaper.Contains(q.Id)))
                    .ToList(),
                Lessons = lessons,
            };
        }
    }
}
