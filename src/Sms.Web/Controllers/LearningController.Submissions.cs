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
    /// doc/Modules/37 §8.4 — the submission tracker — and §8.5 — the marking
    /// queue. Two views, one permission (<c>LRN/Marking</c>), because §6 gives
    /// the queue one row and the tracker none of its own: they are the read and
    /// write faces of the same question about the same class.
    ///
    /// <para>
    /// Kept in a partial so §8.1-3's controller body is untouched, and the port
    /// arrives through <c>[FromServices]</c> for the same reason — following
    /// <c>FeesController.StudentDiscounts</c> and <c>PaymentsController.Accounts</c>,
    /// which extend a controller without editing a constructor several slices
    /// share.
    /// </para>
    ///
    /// <para>
    /// <b>A refusal here is never raw.</b> Every path funnels through
    /// <see cref="UserMessage.For(Exception, bool)"/>, which already carries all
    /// eight of this slice's refusals in both languages. And every GET that the
    /// teacher does not reach answers <c>NotFound</c> rather than an explanation
    /// (BR-SEC-010): "you may not open 3-B's tracker" tells someone that 3-B has
    /// homework, which is itself the leak.
    /// </para>
    ///
    /// <para>
    /// The same DEVIATION as §8.1-3 applies: <c>hasSchoolWideReach</c> is always
    /// false, because BR-LRN-002's Vice-Principal reach needs a data-scoped
    /// permission (BR-GLB-071) this screen will not invent. Deny by default.
    /// </para>
    /// </summary>
    public partial class LearningController
    {
        // ---------------------------------------------------------------- §8.4 the tracker

        [HttpGet("homework/{id:int}/submissions")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Marking, ActionVerb.View)]
        public async Task<IActionResult> Submissions(int id, [FromServices] IHomeworkSubmissionAdmin submissions)
        {
            var m = await BuildMarkingAsync(id, submissions);
            return m == null ? NotFound() : View(m);
        }

        /// <summary>
        /// §8.4's "one-click chase". The selection is posted rather than assumed:
        /// a teacher who has already spoken to two of the five missing students
        /// should not have to message them again to reach the other three.
        /// </summary>
        [HttpPost("homework/{id:int}/chase")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Marking, ActionVerb.Edit)]
        public async Task<IActionResult> Chase(int id, int[] enrollmentIds, [FromServices] IHomeworkSubmissionAdmin submissions)
        {
            if (enrollmentIds == null || enrollmentIds.Length == 0)
            {
                TempData["Error"] = T("Choose who to remind first.", "اختر من تريد تذكيره أولاً.");
                return RedirectToAction(nameof(Submissions), new { id });
            }

            try
            {
                var chased = await submissions.ChaseAsync(
                    id, enrollmentIds, cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = chased == 0
                    // Not an error: the roster is live, and work that landed
                    // between rendering the page and pressing the button is the
                    // good outcome, not a failure.
                    ? T("Nobody was reminded — their work has since arrived.",
                        "لم يُذكَّر أحد — فقد وصل عملهم بعد فتح الصفحة.")
                    : T($"{chased} student(s) and their families were reminded.",
                        $"جرى تذكير {chased} من الطلاب وأسرهم.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Submissions), new { id });
        }

        // ---------------------------------------------------------------- §8.5 the marking queue

        [HttpGet("homework/{id:int}/marking")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Marking, ActionVerb.View)]
        public async Task<IActionResult> Marking(int id, [FromServices] IHomeworkSubmissionAdmin submissions)
        {
            var m = await BuildMarkingAsync(id, submissions);
            return m == null ? NotFound() : View(m);
        }

        /// <summary>§4's Collecting -> Marking step: the teacher says the queue is closed to their own attention, not that the door is shut (BR-LRN-005 keeps late work acceptable).</summary>
        [HttpPost("homework/{id:int}/begin-marking")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Marking, ActionVerb.Edit)]
        public async Task<IActionResult> BeginMarking(int id, [FromServices] IHomeworkSubmissionAdmin submissions)
        {
            try
            {
                await submissions.BeginMarkingAsync(id, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Marking started. Late work is still accepted.", "بدأ التصحيح. والعمل المتأخر ما زال مقبولاً.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Marking), new { id });
        }

        /// <summary>
        /// P-SHEET's single submit: the whole sheet at once, following
        /// <c>GradingController.SaveMarks</c> — the product's other marks grid —
        /// down to reading the form by row key and parsing invariant.
        ///
        /// <para>
        /// A blank box is "not marked yet", never zero. That distinction is the
        /// one BR-LRN-011 counts on when it refuses to release, so turning an
        /// empty cell into a 0 here would quietly satisfy a gate that exists to
        /// stop exactly that.
        /// </para>
        /// </summary>
        [HttpPost("homework/{id:int}/marks")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Marking, ActionVerb.Edit)]
        public async Task<IActionResult> SaveMarks(int id, [FromServices] IHomeworkSubmissionAdmin submissions)
        {
            try
            {
                var roster = await submissions.RosterAsync(id, cancellationToken: HttpContext.RequestAborted);
                var saved = 0;
                var rejected = new List<string>();

                foreach (var row in roster.Where(r => r.HasSubmitted))
                {
                    var raw = Request.Form[$"score_{row.SubmissionId}"].ToString().Trim();
                    var feedback = Request.Form[$"feedback_{row.SubmissionId}"].ToString().Trim();

                    decimal? score = null;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        // Invariant, like the marksheet: an Arabic locale posts a
                        // decimal the current culture would not read back.
                        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                        {
                            rejected.Add(IsArabic ? row.StudentNameAr : row.StudentNameEn);
                            continue;
                        }

                        score = parsed;
                    }

                    // Unchanged rows are skipped rather than re-saved: the sheet
                    // posts thirty rows and a teacher usually touched three, and
                    // every save is an audited write (BR-LRN-015).
                    if (score == row.Score && string.Equals(feedback, row.Feedback ?? string.Empty, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    await submissions.ScoreAsync(
                        row.SubmissionId!.Value,
                        score,
                        string.IsNullOrWhiteSpace(feedback) ? null : feedback,
                        cancellationToken: HttpContext.RequestAborted);

                    saved++;
                }

                if (rejected.Count > 0)
                {
                    TempData["Error"] = T(
                        $"{rejected.Count} mark(s) were not numbers and were left alone: {string.Join(", ", rejected)}.",
                        $"رُفضت {rejected.Count} درجة لأنها ليست أرقاماً وتُركت كما هي: {string.Join("، ", rejected)}.");
                }

                if (saved > 0)
                {
                    TempData["Flash"] = T($"{saved} mark(s) saved.", $"حُفظت {saved} درجة.");
                }
                else if (rejected.Count == 0)
                {
                    TempData["Flash"] = T("Nothing had changed.", "لم يتغيّر شيء.");
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Marking), new { id });
        }

        /// <summary>
        /// BR-LRN-012: hands the raw marks to Module 17 and moves the homework to
        /// Released, which is terminal here. The engine's gate decides whether it
        /// may happen; this action only translates the answer.
        /// </summary>
        [HttpPost("homework/{id:int}/release")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Marking, ActionVerb.Edit)]
        public async Task<IActionResult> Release(int id, [FromServices] IHomeworkSubmissionAdmin submissions)
        {
            try
            {
                await submissions.ReleaseAsync(id, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T(
                    "Marks released to the gradebook. Publication is the marksheet's own step.",
                    "رُصدت الدرجات في سجل الدرجات. والنشر خطوة كشف الدرجات نفسه.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Marking), new { id });
        }

        // ---------------------------------------------------------------- shared

        /// <summary>
        /// Null when the homework does not exist or the signed-in user does not
        /// reach it — the caller answers <c>NotFound</c> either way, so the two
        /// are indistinguishable from outside (BR-SEC-010).
        /// </summary>
        private async Task<HomeworkMarkingViewModel?> BuildMarkingAsync(int id, IHomeworkSubmissionAdmin submissions)
        {
            var homework = await _db.Homeworks.AsNoTracking()
                .SingleOrDefaultAsync(h => h.Id == id, HttpContext.RequestAborted);
            if (homework == null)
            {
                return null;
            }

            IReadOnlyList<HomeworkRosterRow> roster;
            try
            {
                roster = await submissions.RosterAsync(id, cancellationToken: HttpContext.RequestAborted);
            }
            catch (TeachingReachException)
            {
                return null;
            }

            // Looked up rather than picked: a retired subject must not take this
            // screen down for work already set against it (SoftActiveLookupTests).
            var subject = await (
                from o in _db.CurriculumOfferings.IgnoreQueryFilters().AsNoTracking()
                join s in _db.Subjects.IgnoreQueryFilters().AsNoTracking() on o.SubjectId equals s.Id
                where o.Id == homework.CurriculumOfferingId && s.SchoolId == _db.CurrentSchoolId
                select IsArabic ? s.Name.NameAr : s.Name.NameEn)
                .SingleOrDefaultAsync(HttpContext.RequestAborted);

            var section = await _db.Sections.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == homework.SectionId && s.SchoolId == _db.CurrentSchoolId)
                .Select(s => IsArabic ? s.NameAr : s.NameEn)
                .SingleOrDefaultAsync(HttpContext.RequestAborted);

            string? component = null;
            if (homework.BlueprintComponentId is int componentId)
            {
                component = await _db.BlueprintComponents.AsNoTracking()
                    .Where(c => c.Id == componentId)
                    .Select(c => IsArabic ? c.NameAr : c.NameEn)
                    .SingleOrDefaultAsync(HttpContext.RequestAborted);
            }

            return new HomeworkMarkingViewModel
            {
                HomeworkId = homework.Id,
                Title = IsArabic ? homework.TitleAr : homework.TitleEn,
                ClassLabel = $"{subject} · {section}",
                DueDate = homework.DueDate,
                MaxMarks = homework.MaxMarks,
                BlueprintComponentId = homework.BlueprintComponentId,
                ComponentLabel = component,
                Status = homework.Status,
                LatenessPolicy = homework.LatenessPolicy,
                LatePenaltyPercent = homework.LatePenaltyPercent,
                Roster = roster,
            };
        }
    }
}
