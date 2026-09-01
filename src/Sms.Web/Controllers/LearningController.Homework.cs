using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Learning;
using Sms.Application.Security;
using Sms.Domain.Learning;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/37 §8.3 — the homework desk.
    ///
    /// <para>
    /// Kept in a partial so §8.1-2's controller body is untouched. §8.4 (the
    /// submission tracker) and §8.5 (the marking queue) are absent because
    /// <c>HomeworkSubmission</c> does not exist yet: with nothing submitted there
    /// is no roster to chase and no queue to mark. They are later slices, not
    /// silent omissions.
    /// </para>
    ///
    /// <para>
    /// The same DEVIATION as §8.1-2 applies: BR-LRN-002's Vice-Principal
    /// school-wide reach is granted to nobody here, because expressing "VP and
    /// above" needs a data-scoped permission (BR-GLB-071) this screen will not
    /// invent. <c>hasSchoolWideReach</c> is always false — deny by default.
    /// </para>
    /// </summary>
    public partial class LearningController
    {
        // ---------------------------------------------------------------- §8.3 homework desk

        [HttpGet("homework")]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.View)]
        public async Task<IActionResult> Homework(int? offeringId = null, int? sectionId = null)
        {
            return View(await BuildDeskAsync(offeringId, sectionId));
        }

        [HttpPost("homework/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Create)]
        public async Task<IActionResult> CreateHomework(
            int offeringId, int sectionId, string? titleAr, string? titleEn,
            DateTime dueDate, string? instructionsAr, string? instructionsEn,
            decimal? maxMarks, int? blueprintComponentId,
            LatenessPolicy latenessPolicy, decimal? latePenaltyPercent)
        {
            if (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn))
            {
                // BR-GLB-001: both names before the record exists, not after.
                TempData["Error"] = T("Homework needs both an Arabic and an English title.", "الواجب يحتاج عنواناً عربياً وآخر إنجليزياً.");
                return RedirectToAction(nameof(Homework), new { offeringId, sectionId });
            }

            try
            {
                await _homework.CreateAsync(
                    offeringId, sectionId, titleAr.Trim(), titleEn.Trim(), dueDate,
                    string.IsNullOrWhiteSpace(instructionsAr) ? null : instructionsAr.Trim(),
                    string.IsNullOrWhiteSpace(instructionsEn) ? null : instructionsEn.Trim(),
                    maxMarks, blueprintComponentId, latenessPolicy, latePenaltyPercent,
                    cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = T(
                    "Drafted. The class cannot see it until you issue it.",
                    "حُفظت مسوّدة الواجب، ولا يراها الصف حتى تُكلِّفه بها.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Homework), new { offeringId, sectionId });
        }

        /// <summary>
        /// §8.3. Editing is separate from issuing on purpose: fixing a typo in
        /// the instructions is not the same act as telling a class to do the
        /// work. BR-LRN-016 refuses this once the homework is withdrawn, and
        /// BR-LRN-012 once it is released.
        /// </summary>
        [HttpPost("homework/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Edit)]
        public async Task<IActionResult> EditHomework(
            int id, int offeringId, int sectionId, string? titleAr, string? titleEn,
            DateTime dueDate, string? instructionsAr, string? instructionsEn,
            decimal? maxMarks, int? blueprintComponentId,
            LatenessPolicy latenessPolicy, decimal? latePenaltyPercent)
        {
            if (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn))
            {
                TempData["Error"] = T("Homework needs both an Arabic and an English title.", "الواجب يحتاج عنواناً عربياً وآخر إنجليزياً.");
                return RedirectToAction(nameof(Homework), new { offeringId, sectionId });
            }

            try
            {
                await _homework.UpdateAsync(
                    id, titleAr.Trim(), titleEn.Trim(), dueDate,
                    string.IsNullOrWhiteSpace(instructionsAr) ? null : instructionsAr.Trim(),
                    string.IsNullOrWhiteSpace(instructionsEn) ? null : instructionsEn.Trim(),
                    maxMarks, blueprintComponentId, latenessPolicy, latePenaltyPercent,
                    cancellationToken: HttpContext.RequestAborted);

                TempData["Flash"] = T("Saved.", "حُفظ.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Homework), new { offeringId, sectionId });
        }

        /// <summary>BR-LRN-003/004: issue is the event the section's families see, and the gate runs here.</summary>
        [HttpPost("homework/{id:int}/issue")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Approve)]
        public async Task<IActionResult> IssueHomework(int id, int offeringId, int sectionId)
        {
            try
            {
                await _homework.IssueAsync(id, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Issued. The class and their families can see it now.", "كُلِّف الصف بالواجب، وصار ظاهراً له ولأسره الآن.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Homework), new { offeringId, sectionId });
        }

        /// <summary>BR-LRN-016: withdrawn, never deleted, and the reason is required because anyone who submitted is told it.</summary>
        [HttpPost("homework/{id:int}/withdraw")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, ActionVerb.Deactivate)]
        public async Task<IActionResult> WithdrawHomework(int id, int offeringId, int sectionId, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("Say why the work is being withdrawn.", "اذكر سبب سحب الواجب.");
                return RedirectToAction(nameof(Homework), new { offeringId, sectionId });
            }

            try
            {
                await _homework.WithdrawAsync(id, reason.Trim(), cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Withdrawn. It stays readable as history.", "سُحب الواجب، ويبقى مقروءاً في السجل.");
            }
            catch (ArgumentException)
            {
                TempData["Error"] = T("Say why the work is being withdrawn.", "اذكر سبب سحب الواجب.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Homework), new { offeringId, sectionId });
        }

        // ---------------------------------------------------------------- build

        private async Task<HomeworkDeskViewModel> BuildDeskAsync(int? offeringId, int? sectionId)
        {
            var reachable = await _homework.ReachableSectionsAsync(cancellationToken: HttpContext.RequestAborted);

            var offeringIds = reachable.Select(r => r.CurriculumOfferingId).Distinct().ToList();
            var sectionIds = reachable.Select(r => r.SectionId).Distinct().ToList();

            // Subjects and sections are looked up, not picked, so the soft-active
            // filter is ignored: a retired subject must not take this screen down
            // for every class that still studies it (SoftActiveLookupTests).
            var subjectByOffering = await (
                from o in _db.CurriculumOfferings.IgnoreQueryFilters().AsNoTracking()
                join s in _db.Subjects.IgnoreQueryFilters().AsNoTracking() on o.SubjectId equals s.Id
                where offeringIds.Contains(o.Id) && s.SchoolId == _db.CurrentSchoolId
                select new { o.Id, s.Name.NameAr, s.Name.NameEn })
                .ToDictionaryAsync(x => x.Id, x => IsArabic ? x.NameAr : x.NameEn, HttpContext.RequestAborted);

            var sections = await _db.Sections.IgnoreQueryFilters().AsNoTracking()
                .Where(s => sectionIds.Contains(s.Id) && s.SchoolId == _db.CurrentSchoolId)
                .Select(s => new { s.Id, s.NameAr, s.NameEn })
                .ToDictionaryAsync(x => x.Id, x => IsArabic ? x.NameAr : x.NameEn, HttpContext.RequestAborted);

            var options = reachable
                .Where(r => subjectByOffering.ContainsKey(r.CurriculumOfferingId) && sections.ContainsKey(r.SectionId))
                .Select(r => new SectionOption(
                    r.CurriculumOfferingId,
                    r.SectionId,
                    $"{subjectByOffering[r.CurriculumOfferingId]} · {sections[r.SectionId]}"))
                .OrderBy(o => o.Label, StringComparer.CurrentCulture)
                .ToList();

            var m = new HomeworkDeskViewModel
            {
                Sections = options,
                DefaultDueDate = DateTime.UtcNow.Date.AddDays(1),
            };

            // A pair is selected only when the user actually reaches it — a
            // hand-typed query string cannot widen what the screen shows.
            var chosen = offeringId is int o2 && sectionId is int s2
                && reachable.Any(r => r.CurriculumOfferingId == o2 && r.SectionId == s2)
                ? (o2, s2)
                : options.Count == 1 ? (options[0].OfferingId, options[0].SectionId) : ((int, int)?)null;

            if (chosen is not (int offering, int section))
            {
                return m;
            }

            m.SelectedOfferingId = offering;
            m.SelectedSectionId = section;

            // BR-LRN-004: only this offering's own blueprint components.
            m.Components = await (
                from c in _db.BlueprintComponents.AsNoTracking()
                join b in _db.Blueprints.AsNoTracking() on c.BlueprintId equals b.Id
                where b.CurriculumOfferingId == offering
                select new ComponentOption(c.Id, IsArabic ? c.NameAr : c.NameEn, c.MaxScore))
                .ToListAsync(HttpContext.RequestAborted);

            var today = DateTime.UtcNow.Date;
            var rows = await _db.Homeworks.AsNoTracking()
                .Where(h => h.CurriculumOfferingId == offering && h.SectionId == section)
                .OrderBy(h => h.DueDate)
                .ToListAsync(HttpContext.RequestAborted);

            m.Rows = rows
                .Select(h => new HomeworkRow(
                    h,
                    IsArabic ? h.TitleAr : h.TitleEn,
                    h.DueDate.Date < today && HomeworkStatusTransitions.AcceptsSubmissions(h.Status)))
                .ToList();

            return m;
        }
    }
}
