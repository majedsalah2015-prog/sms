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
    /// doc/Modules/37 §8.6 — the question bank, and the first screen of the
    /// module's online-exam half.
    ///
    /// <para>
    /// Three views for one permission: the offering's banks, one bank's
    /// questions, and the authoring form. They are one screen in §8's numbering
    /// and one grant in §6, split only because a question with dynamic options is
    /// a page rather than a row.
    /// </para>
    ///
    /// <para>
    /// <b>What §8.6 asks for and this does not yet do: the usage count.</b> "Usage
    /// count" means how many papers have drawn on a question, and
    /// <c>PaperItem</c> arrives with §8.7. Rather than showing a zero that looks
    /// like an answer, the bank shows the version count — which is real, is the
    /// other half of BR-LRN-007, and is what an author revising a question
    /// actually wants to see. The usage column arrives with the papers that would
    /// fill it.
    /// </para>
    ///
    /// <para>
    /// The same DEVIATION as §8.1-8.5: <c>hasSchoolWideReach</c> is always false,
    /// because BR-LRN-002's Vice-Principal reach needs a data-scoped permission
    /// (BR-GLB-071) this screen will not invent. Deny by default.
    /// </para>
    /// </summary>
    public partial class LearningController
    {
        // ---------------------------------------------------------------- the banks

        [HttpGet("question-banks")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.View)]
        public async Task<IActionResult> QuestionBanks(
            int? offeringId,
            bool includeRetired,
            [FromServices] IQuestionBankAdmin banks,
            [FromServices] Application.Common.Interfaces.ICurrentUser user)
        {
            return View(await BuildBanksAsync(offeringId, includeRetired, banks, user));
        }

        [HttpPost("question-banks/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.Create)]
        public async Task<IActionResult> CreateQuestionBank(
            int offeringId, string? nameAr, string? nameEn, QuestionShareScope shareScope,
            [FromServices] IQuestionBankAdmin banks)
        {
            if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
            {
                // BR-GLB-001: both names before the record exists, not after.
                TempData["Error"] = T("A bank needs both an Arabic and an English name.", "البنك يحتاج اسماً عربياً وآخر إنجليزياً.");
                return RedirectToAction(nameof(QuestionBanks), new { offeringId });
            }

            try
            {
                var bank = await banks.CreateBankAsync(
                    offeringId, nameAr, nameEn, shareScope, cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = T("Bank created.", "أُنشئ البنك.");
                return RedirectToAction(nameof(QuestionBankDetail), new { id = bank.Id });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(QuestionBanks), new { offeringId });
        }

        [HttpPost("question-banks/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.Edit)]
        public async Task<IActionResult> EditQuestionBank(
            int id, string? nameAr, string? nameEn, QuestionShareScope shareScope,
            [FromServices] IQuestionBankAdmin banks)
        {
            if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
            {
                TempData["Error"] = T("A bank needs both an Arabic and an English name.", "البنك يحتاج اسماً عربياً وآخر إنجليزياً.");
                return RedirectToAction(nameof(QuestionBankDetail), new { id });
            }

            try
            {
                await banks.UpdateBankAsync(id, nameAr, nameEn, shareScope, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Bank updated.", "حُدِّث البنك.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(QuestionBankDetail), new { id });
        }

        /// <summary>BR-GLB-005/BR-GLB-006: retired, never deleted — its questions may sit on a paper somebody has answered.</summary>
        [HttpPost("question-banks/{id:int}/retire")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.Deactivate)]
        public async Task<IActionResult> RetireQuestionBank(int id, [FromServices] IQuestionBankAdmin banks)
        {
            try
            {
                await banks.RetireBankAsync(id, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Bank retired. Its questions stay on every paper that used them.", "تقاعد البنك. وتبقى أسئلته على كلّ ورقة استُعملت فيها.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(QuestionBankDetail), new { id });
        }

        // ---------------------------------------------------------------- one bank

        [HttpGet("question-banks/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.View)]
        public async Task<IActionResult> QuestionBankDetail(
            int id, QuestionType? type, QuestionDifficulty? difficulty, bool includeDeprecated,
            [FromServices] IQuestionBankAdmin banks)
        {
            var m = await BuildBankAsync(id, type, difficulty, includeDeprecated, banks);
            return m == null ? NotFound() : View("QuestionBank", m);
        }

        [HttpPost("questions/{id:int}/deprecate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeprecateQuestion(
            int id, int bankId, string? reason, [FromServices] IQuestionBankAdmin banks)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("Say why the question is being withdrawn — the next author to wonder deserves the answer.",
                    "اذكر سبب سحب السؤال — فمن يتساءل بعدك يستحقّ الجواب.");
                return RedirectToAction(nameof(QuestionBankDetail), new { id = bankId });
            }

            try
            {
                await banks.DeprecateQuestionAsync(id, reason, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Question withdrawn from future papers.", "سُحب السؤال من الأوراق القادمة.");
            }
            catch (ArgumentException)
            {
                TempData["Error"] = T("Say why the question is being withdrawn.", "اذكر سبب سحب السؤال.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(QuestionBankDetail), new { id = bankId });
        }

        // ---------------------------------------------------------------- authoring

        [HttpGet("question-banks/{id:int}/questions/new")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.Create)]
        public async Task<IActionResult> NewQuestion(int id, [FromServices] IQuestionBankAdmin banks)
        {
            var m = await BuildEditorAsync(id, null, banks);
            return m == null ? NotFound() : View("QuestionEdit", m);
        }

        [HttpGet("questions/{id:int}/revise")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.Edit)]
        public async Task<IActionResult> ReviseQuestion(int id, [FromServices] IQuestionBankAdmin banks)
        {
            var question = await _db.Questions.AsNoTracking()
                .SingleOrDefaultAsync(q => q.Id == id, HttpContext.RequestAborted);
            if (question == null) { return NotFound(); }

            var m = await BuildEditorAsync(question.QuestionBankId, question, banks);
            return m == null ? NotFound() : View("QuestionEdit", m);
        }

        /// <summary>
        /// Adds version 1, or — when <paramref name="reviseQuestionId"/> is given —
        /// version N+1 (BR-LRN-007). One action for both because it is one form
        /// and one draft: a revision that took a different shape from a creation
        /// is how version two ends up unable to say something version one could.
        /// </summary>
        [HttpPost("question-banks/{id:int}/questions/save")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.QuestionBank, ActionVerb.Create)]
        public async Task<IActionResult> SaveQuestion(
            int id, int? reviseQuestionId, QuestionType type, string? stemAr, string? stemEn,
            string? marks, QuestionDifficulty difficulty, int? lessonId, string? tolerance,
            string? explanationAr, string? explanationEn,
            [FromServices] IQuestionBankAdmin banks)
        {
            if (string.IsNullOrWhiteSpace(stemAr) || string.IsNullOrWhiteSpace(stemEn))
            {
                TempData["Error"] = T("A question needs its text in both languages — a bilingual school sits both.",
                    "السؤال يحتاج نصّه بالعربية والإنجليزية — فالمدرسة ثنائية اللغة تمتحن باللغتين.");
                return Redirect(BackToEditor(id, reviseQuestionId));
            }

            // Invariant, like every other decimal the product posts: an Arabic
            // locale sends a number the current culture will not read back.
            if (!decimal.TryParse((marks ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var marksValue))
            {
                TempData["Error"] = T("The mark must be a number.", "الدرجة يجب أن تكون رقماً.");
                return Redirect(BackToEditor(id, reviseQuestionId));
            }

            decimal? toleranceValue = null;
            if (!string.IsNullOrWhiteSpace(tolerance))
            {
                if (!decimal.TryParse(tolerance.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedTolerance))
                {
                    TempData["Error"] = T("The tolerance must be a number.", "هامش الخطأ يجب أن يكون رقماً.");
                    return Redirect(BackToEditor(id, reviseQuestionId));
                }

                toleranceValue = parsedTolerance;
            }

            var draft = new QuestionDraft
            {
                Type = type,
                StemAr = stemAr,
                StemEn = stemEn,
                Marks = marksValue,
                Difficulty = difficulty,
                LessonId = lessonId,
                NumericTolerance = toleranceValue,
                ExplanationAr = explanationAr,
                ExplanationEn = explanationEn,
                Options = ReadOptions(),
                AcceptedAnswers = ReadAcceptedAnswers(),
            };

            try
            {
                if (reviseQuestionId is int revising)
                {
                    await banks.ReviseQuestionAsync(revising, draft, cancellationToken: HttpContext.RequestAborted);
                    TempData["Flash"] = T("A new version was saved. The previous one stays on every paper that used it.",
                        "حُفظت نسخة جديدة. وتبقى السابقة على كلّ ورقة استُعملت فيها.");
                }
                else
                {
                    await banks.AddQuestionAsync(id, draft, cancellationToken: HttpContext.RequestAborted);
                    TempData["Flash"] = T("Question added.", "أُضيف السؤال.");
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return Redirect(BackToEditor(id, reviseQuestionId));
            }

            return RedirectToAction(nameof(QuestionBankDetail), new { id });
        }

        // ---------------------------------------------------------------- shared

        private string BackToEditor(int bankId, int? reviseQuestionId)
            => reviseQuestionId is int revising
                ? Url.Action(nameof(ReviseQuestion), new { id = revising })!
                : Url.Action(nameof(NewQuestion), new { id = bankId })!;

        /// <summary>
        /// The option rows the form posted, in order. Read from the form rather
        /// than model-bound because the count is the author's to decide and an
        /// empty row is a row they abandoned, not an option they meant.
        /// </summary>
        private IReadOnlyList<QuestionDraftOption> ReadOptions()
        {
            var options = new List<QuestionDraftOption>();

            for (var i = 0; i < 12; i++)
            {
                var ar = Request.Form[$"optionAr_{i}"].ToString().Trim();
                var en = Request.Form[$"optionEn_{i}"].ToString().Trim();

                if (string.IsNullOrWhiteSpace(ar) && string.IsNullOrWhiteSpace(en))
                {
                    continue;
                }

                options.Add(new QuestionDraftOption(
                    ar,
                    // A bilingual school still writes "H2O" once. Falling back
                    // rather than refusing keeps the author from typing an
                    // untranslatable option twice.
                    string.IsNullOrWhiteSpace(en) ? ar : en,
                    Request.Form[$"optionCorrect_{i}"].Count > 0));
            }

            return options;
        }

        private IReadOnlyList<string> ReadAcceptedAnswers()
            => (Request.Form["acceptedAnswers"].ToString() ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => a.Length > 0)
                .ToList();

        private async Task<QuestionBanksViewModel> BuildBanksAsync(
            int? offeringId, bool includeRetired, IQuestionBankAdmin banks,
            Application.Common.Interfaces.ICurrentUser user)
        {
            var reachable = await banks.ReachableOfferingsAsync(cancellationToken: HttpContext.RequestAborted);

            var labels = await OfferingLabelsAsync(reachable);

            var m = new QuestionBanksViewModel
            {
                Offerings = labels,
                IncludeRetired = includeRetired,
            };

            var chosen = offeringId is int o && reachable.Contains(o)
                ? o
                : labels.Count == 1 ? labels[0].Id : (int?)null;

            if (chosen is not int offering)
            {
                return m;
            }

            m.SelectedOfferingId = offering;

            var rows = await banks.BanksAsync(offering, includeRetired, cancellationToken: HttpContext.RequestAborted);
            var bankIds = rows.Select(b => b.Id).ToList();

            var counts = await _db.Questions.AsNoTracking()
                .Where(q => bankIds.Contains(q.QuestionBankId) && q.IsCurrentVersion)
                .GroupBy(q => new { q.QuestionBankId, q.IsDeprecated })
                .Select(g => new { g.Key.QuestionBankId, g.Key.IsDeprecated, Count = g.Count() })
                .ToListAsync(HttpContext.RequestAborted);

            m.Banks = rows
                .Select(b => new QuestionBankRow(
                    b,
                    IsArabic ? b.NameAr : b.NameEn,
                    counts.Where(c => c.QuestionBankId == b.Id && !c.IsDeprecated).Sum(c => c.Count),
                    counts.Where(c => c.QuestionBankId == b.Id && c.IsDeprecated).Sum(c => c.Count),
                    b.CreatedByUserId == user.UserId))
                .ToList();

            return m;
        }

        private async Task<QuestionBankViewModel?> BuildBankAsync(
            int bankId, QuestionType? type, QuestionDifficulty? difficulty, bool includeDeprecated,
            IQuestionBankAdmin banks)
        {
            QuestionBank bank;
            IReadOnlyList<Question> questions;
            try
            {
                bank = await _db.QuestionBanks.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(b => b.Id == bankId && b.SchoolId == _db.CurrentSchoolId, HttpContext.RequestAborted);

                questions = await banks.QuestionsAsync(
                    bankId, type, difficulty, includeDeprecated, cancellationToken: HttpContext.RequestAborted);
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

            var questionIds = questions.Select(q => q.Id).ToList();
            var roots = questions.Select(q => q.RootQuestionId).ToList();

            var optionCounts = await _db.QuestionOptions.AsNoTracking()
                .Where(o => questionIds.Contains(o.QuestionId))
                .GroupBy(o => o.QuestionId)
                .Select(g => new { QuestionId = g.Key, Count = g.Count() })
                .ToListAsync(HttpContext.RequestAborted);

            var versionCounts = await _db.Questions.AsNoTracking()
                .Where(q => roots.Contains(q.RootQuestionId))
                .GroupBy(q => q.RootQuestionId)
                .Select(g => new { Root = g.Key, Count = g.Count() })
                .ToListAsync(HttpContext.RequestAborted);

            var offeringLabel = (await OfferingLabelsAsync(new[] { bank.CurriculumOfferingId }))
                .FirstOrDefault()?.Label ?? string.Empty;

            return new QuestionBankViewModel
            {
                Bank = bank,
                BankName = IsArabic ? bank.NameAr : bank.NameEn,
                OfferingLabel = offeringLabel,
                FilterType = type,
                FilterDifficulty = difficulty,
                IncludeDeprecated = includeDeprecated,
                Questions = questions
                    .Select(q => new QuestionRow(
                        q,
                        IsArabic ? q.StemAr : q.StemEn,
                        optionCounts.FirstOrDefault(c => c.QuestionId == q.Id)?.Count ?? 0,
                        versionCounts.FirstOrDefault(c => c.Root == q.RootQuestionId)?.Count ?? 1))
                    .ToList(),
            };
        }

        private async Task<QuestionEditViewModel?> BuildEditorAsync(
            int bankId, Question? revising, IQuestionBankAdmin banks)
        {
            QuestionBank bank;
            try
            {
                // Reached through the port so BR-LRN-002 is applied by the same
                // code the writes use, rather than by a second copy here.
                var reachable = await banks.ReachableOfferingsAsync(cancellationToken: HttpContext.RequestAborted);

                bank = await _db.QuestionBanks.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(b => b.Id == bankId && b.SchoolId == _db.CurrentSchoolId, HttpContext.RequestAborted);

                if (!reachable.Contains(bank.CurriculumOfferingId))
                {
                    return null;
                }
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            var m = new QuestionEditViewModel
            {
                QuestionBankId = bankId,
                BankName = IsArabic ? bank.NameAr : bank.NameEn,
                Revising = revising,
            };

            if (revising != null)
            {
                var (options, answers) = await banks.DetailAsync(revising.Id, cancellationToken: HttpContext.RequestAborted);
                m.Options = options;
                m.AcceptedAnswers = answers;
                m.Versions = await banks.VersionsAsync(revising.RootQuestionId, cancellationToken: HttpContext.RequestAborted);
            }

            // §8.7's topic axis. Looked up rather than picked, so a retired lesson
            // still names itself on a question already filed against it.
            m.Lessons = await _db.Lessons.IgnoreQueryFilters().AsNoTracking()
                .Where(l => l.CurriculumOfferingId == bank.CurriculumOfferingId && l.SchoolId == _db.CurrentSchoolId)
                .OrderBy(l => l.WeekNumber)
                .Select(l => new ValueTuple<int, string>(l.Id, IsArabic ? l.TitleAr : l.TitleEn))
                .ToListAsync(HttpContext.RequestAborted);

            return m;
        }

        /// <summary>
        /// Subject names for a set of offerings. Ignores the soft-active filter:
        /// a retired subject must not take this screen down for a bank that still
        /// belongs to it (SoftActiveLookupTests).
        /// </summary>
        private async Task<IReadOnlyList<OfferingOption>> OfferingLabelsAsync(IReadOnlyCollection<int> offeringIds)
        {
            if (offeringIds.Count == 0)
            {
                return Array.Empty<OfferingOption>();
            }

            var rows = await (
                from o in _db.CurriculumOfferings.IgnoreQueryFilters().AsNoTracking()
                join s in _db.Subjects.IgnoreQueryFilters().AsNoTracking() on o.SubjectId equals s.Id
                where offeringIds.Contains(o.Id) && s.SchoolId == _db.CurrentSchoolId
                select new { o.Id, s.Name.NameAr, s.Name.NameEn })
                .ToListAsync(HttpContext.RequestAborted);

            return rows
                .Select(r => new OfferingOption(r.Id, IsArabic ? r.NameAr : r.NameEn))
                .OrderBy(o => o.Label, StringComparer.CurrentCulture)
                .ToList();
        }
    }
}
