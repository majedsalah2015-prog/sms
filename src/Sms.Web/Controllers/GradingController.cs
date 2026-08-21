using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Grading;
using Sms.Domain.Attendance;
using Sms.Domain.Grading;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/17 §8 — E-302 basic subset over IGradingAdmin: 8.1 Scale
    /// designer, 8.2 Blueprint &amp; weights editor, 8.3 Criteria designer,
    /// 8.4 Marksheet workspace (teacher grid + WF-07 chain + WF-08
    /// correction), 8.5 Results explorer, 8.6 Report card (HTML render —
    /// the immutable PDF of BR-GRA-008 stays blocked on the O6 engine
    /// decision, so this is a print-styled page, not a stored document).
    /// Deferred with their modules: 8.7 transcripts (M18), 8.8 appeals,
    /// 8.9 portal (E-304), rubric/KG modes (BR-GRA-002), comment banks
    /// (BR-GRA-010), HoD distribution charts.
    /// </summary>
    [Route("grading")]
    public class GradingController : Controller
    {
        private readonly IGradingAdmin _grading;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;

        public GradingController(IGradingAdmin grading, AppDbContext db, IAuditContext audit, IWorkingYearContext workingYear)
        {
            _grading = grading;
            _db = db;
            _audit = audit;
            _workingYear = workingYear;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.4 Marksheet workspace — list

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null, MarksheetStatus? status = null, int? section = null)
        {
            var m = new MarksheetListViewModel { Status = status, SectionId = section };
            await FillPageAsync(m, year);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            var sheets = await _db.Marksheets.AsNoTracking().Where(s => s.AcademicYearId == yid).ToListAsync();
            m.CountsByStatus = sheets.GroupBy(s => s.Status).ToDictionary(g => g.Key, g => g.Count());
            if (status != null) sheets = sheets.Where(s => s.Status == status).ToList();
            if (section != null) sheets = sheets.Where(s => s.SectionId == section).ToList();

            var blueprints = await _db.Blueprints.AsNoTracking().Where(b => b.AcademicYearId == yid).ToListAsync();
            var offerings = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.AcademicYearId == yid).ToListAsync();
            var subjects = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var sections = await _db.Sections.AsNoTracking().Where(s => s.AcademicYearId == yid).OrderBy(s => s.NameEn).ToListAsync();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == yid).ToListAsync();
            var sheetIds = sheets.Select(s => s.Id).ToList();
            var entryStats = await _db.MarkEntries.AsNoTracking().Where(e => sheetIds.Contains(e.MarksheetId))
                .GroupBy(e => e.MarksheetId).Select(g => new { g.Key, Total = g.Count(), Resolved = g.Count(e => e.Score != null || e.IsAbsent || e.IsExempt) }).ToListAsync();

            void GradeLevelOf(int profileId, out Sms.Domain.Grades.GradeLevel grade)
            {
                var p = profiles.FirstOrDefault(x => x.Id == profileId);
                grade = p == null ? null! : grades.First(g => g.Id == p.GradeLevelId);
            }

            var rows = new List<MarksheetListViewModel.Row>();
            foreach (var s in sheets.OrderByDescending(s => s.Id))
            {
                var bp = blueprints.FirstOrDefault(b => b.Id == s.BlueprintId); if (bp == null) continue;
                var off = offerings.FirstOrDefault(o => o.Id == bp.CurriculumOfferingId); if (off == null) continue;
                var sec = sections.FirstOrDefault(x => x.Id == s.SectionId); if (sec == null) continue;
                var term = m.Terms.FirstOrDefault(t => t.Id == bp.TermId); if (term == null) continue;
                GradeLevelOf(off.GradeYearProfileId, out var grade);
                var st = entryStats.FirstOrDefault(x => x.Key == s.Id);
                rows.Add(new MarksheetListViewModel.Row(s, subjects.First(x => x.Id == off.SubjectId), grade, term, sec, st?.Total ?? 0, st?.Resolved ?? 0));
            }
            m.Rows = rows;

            m.LockedBlueprints = blueprints.Where(b => b.IsLocked).Select(b =>
            {
                var off = offerings.FirstOrDefault(o => o.Id == b.CurriculumOfferingId); if (off == null) return null;
                var term = m.Terms.FirstOrDefault(t => t.Id == b.TermId); if (term == null) return null;
                GradeLevelOf(off.GradeYearProfileId, out var grade);
                return new MarksheetListViewModel.BlueprintOption(b.Id, subjects.First(x => x.Id == off.SubjectId), grade, term, off.GradeYearProfileId);
            }).Where(x => x != null).Select(x => x!).OrderBy(x => x.Grade?.SequenceOrder).ThenBy(x => x.Subject.Code).ThenBy(x => x.Term.SequenceNumber).ToList();
            m.Sections = sections.Select(s => { GradeLevelOf(s.GradeYearProfileId, out var g); return new MarksheetListViewModel.SectionOption(s, g); }).ToList();
            return View(m);
        }

        [HttpPost("marksheets/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, ActionVerb.Create)]
        public async Task<IActionResult> CreateMarksheet(int? blueprintId, int? sectionId, int? year)
        {
            try
            {
                if (blueprintId == null || sectionId == null) throw new InvalidOperationException(T("Choose a finalized blueprint and a section.", "اختر مخططاً مقفلاً وشعبة."));
                var bp = await _db.Blueprints.AsNoTracking().SingleAsync(b => b.Id == blueprintId);
                var off = await _db.CurriculumOfferings.AsNoTracking().SingleAsync(o => o.Id == bp.CurriculumOfferingId);
                var sec = await _db.Sections.AsNoTracking().SingleAsync(s => s.Id == sectionId);
                if (sec.GradeYearProfileId != off.GradeYearProfileId) throw new InvalidOperationException(T("The section belongs to a different grade than the blueprint's offering.", "الشعبة تتبع صفاً مختلفاً عن مادة المخطط."));
                if (await _db.Marksheets.AnyAsync(s => s.BlueprintId == bp.Id && s.SectionId == sec.Id)) throw new InvalidOperationException(T("A marksheet already exists for this offering, term and section.", "يوجد كشف درجات لهذه المادة والفترة والشعبة مسبقاً."));
                var sheet = await _grading.CreateMarksheetAsync(bp.Id, sec.Id);
                TempData["Flash"] = T("Marksheet created — one row per current section member.", "أُنشئ كشف الدرجات — صف لكل طالب حالي في الشعبة.");
                return RedirectToAction(nameof(Marksheet), new { id = sheet.Id });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { year });
        }

        // ================================================================== 8.4 Marksheet workspace — grid

        [HttpGet("marksheets/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, ActionVerb.View)]
        public async Task<IActionResult> Marksheet(int id)
        {
            var m = await BuildWorkspaceAsync(id);
            return m == null ? NotFound() : View(m);
        }

        [HttpPost("marksheets/{id:int}/save")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, ActionVerb.Edit)]
        public async Task<IActionResult> SaveMarks(int id)
        {
            try
            {
                var sheet = await _db.Marksheets.AsNoTracking().SingleAsync(s => s.Id == id);
                if (sheet.Status != MarksheetStatus.Draft) throw new InvalidOperationException(T("Only a Draft marksheet accepts entry (BR-GRA-005).", "لا يقبل الإدخال إلا كشف بحالة مسودة (BR-GRA-005)."));
                var components = await _db.BlueprintComponents.AsNoTracking().Where(c => c.BlueprintId == sheet.BlueprintId).ToListAsync();
                var entries = await _db.MarkEntries.AsNoTracking().Where(e => e.MarksheetId == id).ToListAsync();
                var inputs = new List<MarkInput>();
                var errors = new List<string>();
                foreach (var e in entries)
                {
                    var key = $"{e.EnrollmentId}_{e.BlueprintComponentId}";
                    var raw = Request.Form[$"score_{key}"].ToString().Trim();
                    var absent = Request.Form[$"absent_{key}"].Count > 0;
                    var exempt = Request.Form[$"exempt_{key}"].Count > 0;
                    decimal? score = null;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) { errors.Add(key); continue; }
                        var max = components.First(c => c.Id == e.BlueprintComponentId).MaxScore;
                        if (v < 0 || v > max) { errors.Add(key + $" ({v} > {max})"); continue; }
                        score = v;
                    }
                    if (absent || exempt) score = null;
                    inputs.Add(new MarkInput(e.BlueprintComponentId, e.EnrollmentId, score, absent, exempt));
                }
                if (errors.Count > 0) throw new InvalidOperationException(T($"{errors.Count} cell(s) rejected — scores must be numeric within 0..max (§9).", $"رُفضت {errors.Count} خلية — الدرجات أرقام ضمن 0..الحد الأقصى (§9)."));
                await _grading.EnterMarksAsync(id, inputs);
                TempData["Flash"] = T("Progress saved.", "تم حفظ التقدم.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Marksheet), new { id });
        }

        [HttpPost("marksheets/{id:int}/status")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, ActionVerb.Submit)]
        public async Task<IActionResult> MarksheetStatusChange(int id, MarksheetStatus target)
        {
            try
            {
                await _grading.ChangeMarksheetStatusAsync(id, target);
                TempData["Flash"] = target == MarksheetStatus.Published
                    ? T("Published — term results computed for every student (BR-GRA-003).", "نُشر — حُسبت نتائج الفترة لكل طالب (BR-GRA-003).")
                    : T($"Marksheet is now {target}.", $"أصبح الكشف بحالة {GradingLabels.MarksheetStatus(target, true)}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Marksheet), new { id });
        }

        [HttpPost("marksheets/{id:int}/correct")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, ActionVerb.Approve)]
        public async Task<IActionResult> CorrectMarksheet(int id, string? reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException(T("A reason is mandatory for a post-publication correction (WF-08).", "السبب إلزامي للتصحيح بعد النشر (WF-08)."));
                await _grading.CorrectPublishedMarksheetAsync(id, reason.Trim());
                TempData["Flash"] = T("Reopened as Draft — re-enter and re-publish; results are recomputed on publish (WF-08).", "أُعيد فتحه كمسودة — أعد الإدخال والنشر؛ تُعاد النتائج عند النشر (WF-08).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Marksheet), new { id });
        }

        [HttpPost("marksheets/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteMarksheet(int id, int? year)
        {
            try
            {
                await _grading.DeleteMarksheetAsync(id);
                TempData["Flash"] = T("Marksheet deleted.", "حُذف كشف الدرجات.");
                return RedirectToAction(nameof(Index), new { year });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Marksheet), new { id });
        }

        // ================================================================== 8.1 Scale designer

        [HttpGet("scales")]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Scales, ActionVerb.View)]
        public async Task<IActionResult> Scales(int? year = null, int? id = null)
        {
            var m = new ScaleDesignerViewModel();
            await FillPageAsync(m, year);
            m.Stages = await _db.Stages.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId).OrderBy(s => s.SequenceOrder).ToListAsync();
            m.Curricula = await LookupAsync("Curriculum");
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;
            var scales = await _db.GradingScales.AsNoTracking().Where(s => s.AcademicYearId == yid).OrderBy(s => s.StageId).ThenBy(s => s.NameEn).ToListAsync();
            var ids = scales.Select(s => s.Id).ToList();
            var bandCounts = await _db.ScaleBands.AsNoTracking().Where(b => ids.Contains(b.GradingScaleId)).GroupBy(b => b.GradingScaleId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var bpCounts = await _db.Blueprints.AsNoTracking().Where(b => ids.Contains(b.GradingScaleId)).GroupBy(b => b.GradingScaleId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            m.Scales = scales.Select(s => new ScaleDesignerViewModel.ScaleRow(s, m.Stages.FirstOrDefault(x => x.Id == s.StageId) ?? new Sms.Domain.Grades.Stage(), bandCounts.FirstOrDefault(x => x.Key == s.Id)?.N ?? 0, bpCounts.FirstOrDefault(x => x.Key == s.Id)?.N ?? 0)).ToList();
            m.Selected = scales.FirstOrDefault(s => s.Id == id) ?? (id == null ? scales.FirstOrDefault() : null);
            if (m.Selected != null)
            {
                m.SelectedStage = m.Stages.FirstOrDefault(s => s.Id == m.Selected.StageId);
                m.Bands = await _db.ScaleBands.AsNoTracking().Where(b => b.GradingScaleId == m.Selected.Id).OrderBy(b => b.SortOrder).ThenByDescending(b => b.MinPercent).ToListAsync();
                m.SelectedBlueprintCount = bpCounts.FirstOrDefault(x => x.Key == m.Selected.Id)?.N ?? 0;
                m.Warnings = BandWarnings(m.Bands);
            }
            return View(m);
        }

        private static IReadOnlyList<string> BandWarnings(IReadOnlyList<ScaleBand> bands)
        {
            var w = new List<string>();
            var ordered = bands.OrderBy(b => b.MinPercent).ToList();
            if (ordered.Count == 0) { w.Add(T("No bands yet — a scale needs at least one passing and one failing band.", "لا توجد نطاقات — يحتاج السلم نطاقاً ناجحاً وآخر راسباً على الأقل.")); return w; }
            if (!ScaleBandResolver.AreNonOverlapping(ordered.Select(b => new ScaleBandResolver.Band(b.Id, b.MinPercent, b.MaxPercent)).ToList())) w.Add(T("Bands overlap — a score would resolve to two bands.", "النطاقات متداخلة — قد تقع الدرجة في نطاقين."));
            if (ordered[0].MinPercent > 0) w.Add(T($"Gap: 0–{ordered[0].MinPercent}% is not covered.", $"فجوة: 0–{ordered[0].MinPercent}% غير مغطاة."));
            for (var i = 1; i < ordered.Count; i++) if (ordered[i].MinPercent - ordered[i - 1].MaxPercent > 0.011m) w.Add(T($"Gap between {ordered[i - 1].MaxPercent}% and {ordered[i].MinPercent}%.", $"فجوة بين {ordered[i - 1].MaxPercent}% و{ordered[i].MinPercent}%."));
            if (ordered[^1].MaxPercent < 100) w.Add(T($"Gap: {ordered[^1].MaxPercent}–100% is not covered.", $"فجوة: {ordered[^1].MaxPercent}–100% غير مغطاة."));
            if (!bands.Any(b => b.IsPassing)) w.Add(T("No passing band.", "لا يوجد نطاق ناجح."));
            if (!bands.Any(b => !b.IsPassing)) w.Add(T("No failing band — every result would pass.", "لا يوجد نطاق راسب — ستنجح كل النتائج."));
            return w;
        }

        [HttpPost("scales/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Scales, ActionVerb.Create)]
        public async Task<IActionResult> CreateScale(int? year, int? stageId, string? nameAr, string? nameEn, int? curriculumId)
        {
            try
            {
                if (stageId == null || string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Stage and both names are required (BR-GLB-001).", "المرحلة والاسمان مطلوبة (BR-GLB-001)."));
                var scale = await _grading.DefineScaleAsync(stageId.Value, nameAr.Trim(), nameEn.Trim(), curriculumId, year ?? _workingYear.AcademicYearId);
                TempData["Flash"] = T("Scale created — add its bands.", "أُنشئ السلم — أضف نطاقاته.");
                return RedirectToAction(nameof(Scales), new { year, id = scale.Id });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Scales), new { year });
        }

        [HttpPost("scales/{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Scales, ActionVerb.Edit)]
        public async Task<IActionResult> EditScale(int id, int? year, string? nameAr, string? nameEn, string? reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Both names are required.", "الاسمان مطلوبان."));
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await _grading.UpdateScaleAsync(id, nameAr.Trim(), nameEn.Trim());
                TempData["Flash"] = T("Scale renamed.", "أُعيدت تسمية السلم.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Scales), new { year, id });
        }

        [HttpPost("scales/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Scales, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteScale(int id, int? year)
        {
            try { await _grading.DeleteScaleAsync(id); TempData["Flash"] = T("Scale deleted.", "حُذف السلم."); return RedirectToAction(nameof(Scales), new { year }); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Scales), new { year, id });
        }

        [HttpPost("scales/{id:int}/lock")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Scales, ActionVerb.Approve)]
        public async Task<IActionResult> LockScale(int id, int? year)
        {
            try
            {
                var bands = await _db.ScaleBands.AsNoTracking().Where(b => b.GradingScaleId == id).ToListAsync();
                var warnings = BandWarnings(bands);
                if (warnings.Count > 0) throw new InvalidOperationException(T("Fix the band warnings before locking: ", "عالج تنبيهات النطاقات قبل القفل: ") + string.Join(" ", warnings));
                await _grading.LockScaleAsync(id);
                TempData["Flash"] = T("Scale locked — bands are now frozen (BR-GRA-001).", "قُفل السلم — النطاقات مجمّدة الآن (BR-GRA-001).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Scales), new { year, id });
        }

        [HttpPost("scales/{id:int}/bands")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Scales, ActionVerb.Edit)]
        public async Task<IActionResult> AddBand(int id, int? year, decimal? min, decimal? max, string? code, string? labelAr, string? labelEn, bool isPassing, int? sortOrder, decimal? gpa)
        {
            try
            {
                ValidateBand(min, max, code, labelAr, labelEn);
                await _grading.AddScaleBandAsync(id, min!.Value, max!.Value, code!.Trim(), labelAr!.Trim(), labelEn!.Trim(), isPassing, sortOrder ?? 0, gpa);
                TempData["Flash"] = T("Band added.", "أُضيف النطاق.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Scales), new { year, id });
        }

        [HttpPost("scales/{id:int}/bands/{bandId:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Scales, ActionVerb.Edit)]
        public async Task<IActionResult> EditBand(int id, int bandId, int? year, decimal? min, decimal? max, string? code, string? labelAr, string? labelEn, bool isPassing, int? sortOrder, decimal? gpa)
        {
            try
            {
                ValidateBand(min, max, code, labelAr, labelEn);
                await _grading.UpdateScaleBandAsync(bandId, min!.Value, max!.Value, code!.Trim(), labelAr!.Trim(), labelEn!.Trim(), isPassing, sortOrder ?? 0, gpa);
                TempData["Flash"] = T("Band updated.", "حُدّث النطاق.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Scales), new { year, id });
        }

        [HttpPost("scales/{id:int}/bands/{bandId:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Scales, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteBand(int id, int bandId, int? year)
        {
            try { await _grading.RemoveScaleBandAsync(bandId); TempData["Flash"] = T("Band removed.", "أُزيل النطاق."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Scales), new { year, id });
        }

        private static void ValidateBand(decimal? min, decimal? max, string? code, string? labelAr, string? labelEn)
        {
            if (min == null || max == null || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(labelAr) || string.IsNullOrWhiteSpace(labelEn)) throw new InvalidOperationException(T("Min, max, code and both labels are required.", "الحد الأدنى والأعلى والرمز والتسميتان مطلوبة."));
            if (min < 0 || max > 100 || min > max) throw new InvalidOperationException(T("Bands must satisfy 0 ≤ min ≤ max ≤ 100.", "يجب أن يكون 0 ≤ الأدنى ≤ الأعلى ≤ 100."));
        }

        // ================================================================== 8.2 Blueprint & weights editor

        [HttpGet("blueprints")]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Blueprints, ActionVerb.View)]
        public async Task<IActionResult> Blueprints(int? year = null, int? profile = null, int? term = null)
        {
            var m = new BlueprintListViewModel();
            await FillPageAsync(m, year);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;
            m.Profile = m.Profiles.FirstOrDefault(p => p.ProfileId == profile) ?? m.Profiles.FirstOrDefault();
            m.Term = m.Terms.FirstOrDefault(t => t.Id == term) ?? m.Terms.FirstOrDefault();
            m.Scales = await _db.GradingScales.AsNoTracking().Where(s => s.AcademicYearId == yid).OrderBy(s => s.NameEn).ToListAsync();
            if (m.Profile == null || m.Term == null) return View(m);

            var offerings = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == m.Profile.ProfileId && o.EffectiveToUtc == null).ToListAsync();
            var subjects = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var offIds = offerings.Select(o => o.Id).ToList();
            var blueprints = await _db.Blueprints.AsNoTracking().Where(b => offIds.Contains(b.CurriculumOfferingId) && b.TermId == m.Term.Id).ToListAsync();
            var bpIds = blueprints.Select(b => b.Id).ToList();
            var comps = await _db.BlueprintComponents.AsNoTracking().Where(c => bpIds.Contains(c.BlueprintId)).ToListAsync();
            var sheetCounts = await _db.Marksheets.AsNoTracking().Where(s => bpIds.Contains(s.BlueprintId)).GroupBy(s => s.BlueprintId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            m.Offerings = offerings.OrderBy(o => subjects.First(s => s.Id == o.SubjectId).Code).Select(o =>
            {
                var bp = blueprints.FirstOrDefault(b => b.CurriculumOfferingId == o.Id);
                var c = bp == null ? new List<BlueprintComponent>() : comps.Where(x => x.BlueprintId == bp.Id).ToList();
                return new BlueprintListViewModel.OfferingRow(o, subjects.First(s => s.Id == o.SubjectId), bp, c.Count, c.Sum(x => x.Weight), bp == null ? 0 : sheetCounts.FirstOrDefault(x => x.Key == bp.Id)?.N ?? 0);
            }).ToList();
            return View(m);
        }

        [HttpPost("blueprints/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Blueprints, ActionVerb.Create)]
        public async Task<IActionResult> CreateBlueprint(int? year, int? profile, int? term, int? offeringId, int? scaleId, bool redistribute)
        {
            try
            {
                if (offeringId == null || term == null || scaleId == null) throw new InvalidOperationException(T("Offering, term and grading scale are required.", "المادة والفترة وسلم التقدير مطلوبة."));
                if (await _db.Blueprints.AnyAsync(b => b.CurriculumOfferingId == offeringId && b.TermId == term)) throw new InvalidOperationException(T("A blueprint already exists for this offering and term.", "يوجد مخطط لهذه المادة والفترة مسبقاً."));
                var bp = await _grading.DefineBlueprintAsync(offeringId.Value, term.Value, scaleId.Value, redistribute);
                TempData["Flash"] = T("Blueprint created — add components until weights sum to 100, then finalize.", "أُنشئ المخطط — أضف المكوّنات حتى يبلغ مجموع الأوزان 100 ثم اعتمده.");
                return RedirectToAction(nameof(Blueprint), new { id = bp.Id });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Blueprints), new { year, profile, term });
        }

        [HttpGet("blueprints/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Blueprints, ActionVerb.View)]
        public async Task<IActionResult> Blueprint(int id)
        {
            var bp = await _db.Blueprints.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id);
            if (bp == null) return NotFound();
            var off = await _db.CurriculumOfferings.AsNoTracking().SingleAsync(o => o.Id == bp.CurriculumOfferingId);
            var profile = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == off.GradeYearProfileId);
            var comps = await _db.BlueprintComponents.AsNoTracking().Where(c => c.BlueprintId == id).OrderBy(c => c.Id).ToListAsync();
            var m = new BlueprintEditorViewModel
            {
                Blueprint = bp, Offering = off, ProfileId = profile.Id,
                Subject = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == off.SubjectId),
                Grade = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId),
                Term = await _db.Terms.AsNoTracking().SingleAsync(t => t.Id == bp.TermId),
                Year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == bp.AcademicYearId),
                Scale = await _db.GradingScales.AsNoTracking().SingleAsync(s => s.Id == bp.GradingScaleId),
                Components = comps, WeightSum = comps.Sum(c => c.Weight),
                MarksheetCount = await _db.Marksheets.AsNoTracking().CountAsync(s => s.BlueprintId == id),
            };
            return View(m);
        }

        [HttpPost("blueprints/{id:int}/components")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Blueprints, ActionVerb.Edit)]
        public async Task<IActionResult> AddComponent(int id, string? nameAr, string? nameEn, decimal? weight, decimal? maxScore)
        {
            try
            {
                ValidateComponent(nameAr, nameEn, weight, maxScore);
                await _grading.AddBlueprintComponentAsync(id, nameAr!.Trim(), nameEn!.Trim(), weight!.Value, maxScore!.Value);
                TempData["Flash"] = T("Component added.", "أُضيف المكوّن.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Blueprint), new { id });
        }

        [HttpPost("blueprints/{id:int}/components/{componentId:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Blueprints, ActionVerb.Edit)]
        public async Task<IActionResult> EditComponent(int id, int componentId, string? nameAr, string? nameEn, decimal? weight, decimal? maxScore)
        {
            try
            {
                ValidateComponent(nameAr, nameEn, weight, maxScore);
                await _grading.UpdateBlueprintComponentAsync(componentId, nameAr!.Trim(), nameEn!.Trim(), weight!.Value, maxScore!.Value);
                TempData["Flash"] = T("Component updated.", "حُدّث المكوّن.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Blueprint), new { id });
        }

        [HttpPost("blueprints/{id:int}/components/{componentId:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Blueprints, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteComponent(int id, int componentId)
        {
            try { await _grading.RemoveBlueprintComponentAsync(componentId); TempData["Flash"] = T("Component removed.", "أُزيل المكوّن."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Blueprint), new { id });
        }

        [HttpPost("blueprints/{id:int}/lock")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Blueprints, ActionVerb.Approve)]
        public async Task<IActionResult> LockBlueprint(int id)
        {
            try { await _grading.LockBlueprintAsync(id); TempData["Flash"] = T("Blueprint finalized — marksheets can now be created from it.", "اعتُمد المخطط — يمكن الآن إنشاء كشوف الدرجات منه."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Blueprint), new { id });
        }

        [HttpPost("blueprints/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Blueprints, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteBlueprint(int id, int? year, int? profile, int? term)
        {
            try { await _grading.DeleteBlueprintAsync(id); TempData["Flash"] = T("Blueprint deleted.", "حُذف المخطط."); return RedirectToAction(nameof(Blueprints), new { year, profile, term }); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Blueprint), new { id });
        }

        private static void ValidateComponent(string? nameAr, string? nameEn, decimal? weight, decimal? maxScore)
        {
            if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn) || weight == null || maxScore == null) throw new InvalidOperationException(T("Both names, weight and max score are required.", "الاسمان والوزن والدرجة القصوى مطلوبة."));
            if (weight <= 0 || weight > 100 || maxScore <= 0) throw new InvalidOperationException(T("Weight must be within 0–100 and max score positive.", "الوزن ضمن 0–100 والدرجة القصوى موجبة."));
        }

        // ================================================================== 8.3 Criteria designer

        [HttpGet("criteria")]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Criteria, ActionVerb.View)]
        public async Task<IActionResult> Criteria(int? year = null)
        {
            var m = new CriteriaDesignerViewModel();
            await FillPageAsync(m, year);
            if (m.Year == null) return View(m);
            var pids = m.Profiles.Select(p => p.ProfileId).ToList();
            var criteria = await _db.PromotionCriteria.AsNoTracking().Where(c => pids.Contains(c.GradeYearProfileId)).ToListAsync();
            var offCounts = await _db.CurriculumOfferings.AsNoTracking().Where(o => pids.Contains(o.GradeYearProfileId) && o.EffectiveToUtc == null && o.IsAssessable).GroupBy(o => o.GradeYearProfileId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var enrollByProfile = await _db.Enrollments.AsNoTracking().Where(e => pids.Contains(e.GradeYearProfileId)).Select(e => new { e.Id, e.GradeYearProfileId }).ToListAsync();
            var yrIds = await _db.YearResults.AsNoTracking().Where(r => r.AcademicYearId == m.Year.Id).Select(r => r.EnrollmentId).ToListAsync();
            m.Rows = m.Profiles.Select(p => new CriteriaDesignerViewModel.Row(p, criteria.FirstOrDefault(c => c.GradeYearProfileId == p.ProfileId), offCounts.FirstOrDefault(x => x.Key == p.ProfileId)?.N ?? 0, enrollByProfile.Count(e => e.GradeYearProfileId == p.ProfileId && yrIds.Contains(e.Id)))).ToList();
            return View(m);
        }

        [HttpPost("criteria/{profileId:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Criteria, ActionVerb.Edit)]
        public async Task<IActionResult> SaveCriteria(int profileId, int? year, decimal? passMark, int? maxFailed, string? reason)
        {
            try
            {
                if (passMark == null || maxFailed == null) throw new InvalidOperationException(T("Pass mark and max failed subjects are required.", "درجة النجاح والحد الأقصى للمواد الراسبة مطلوبان."));
                if (passMark < 0 || passMark > 100 || maxFailed < 0) throw new InvalidOperationException(T("Pass mark must be 0–100; max failed subjects ≥ 0.", "درجة النجاح 0–100؛ الحد الأقصى ≥ 0."));
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await _grading.DefinePromotionCriteriaAsync(profileId, passMark.Value, maxFailed.Value);
                TempData["Flash"] = T("Criteria saved.", "حُفظت المعايير.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Criteria), new { year });
        }

        // ================================================================== 8.5 Results explorer

        [HttpGet("results")]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Results, ActionVerb.View)]
        public async Task<IActionResult> Results(int? year = null, int? section = null, int? term = null)
        {
            var m = new ResultsExplorerViewModel();
            await FillPageAsync(m, year);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;
            var sections = await _db.Sections.AsNoTracking().Where(s => s.AcademicYearId == yid).OrderBy(s => s.NameEn).ToListAsync();
            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == yid).ToListAsync();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            Sms.Domain.Grades.GradeLevel GradeOf(int profileId) { var p = profiles.FirstOrDefault(x => x.Id == profileId); return p == null ? new Sms.Domain.Grades.GradeLevel() : grades.First(g => g.Id == p.GradeLevelId); }
            m.Sections = sections.Select(s => new ResultsExplorerViewModel.SectionOption(s, GradeOf(s.GradeYearProfileId))).ToList();
            m.Section = sections.FirstOrDefault(s => s.Id == section) ?? sections.FirstOrDefault();
            m.Term = m.Terms.FirstOrDefault(t => t.Id == term) ?? m.Terms.FirstOrDefault();
            if (m.Section == null || m.Term == null) return View(m);
            m.Grade = GradeOf(m.Section.GradeYearProfileId); m.ProfileId = m.Section.GradeYearProfileId;
            m.HasCriteria = await _db.PromotionCriteria.AsNoTracking().AnyAsync(c => c.GradeYearProfileId == m.ProfileId);

            var offerings = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == m.ProfileId && o.IsAssessable).ToListAsync();
            var subjects = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId).ToListAsync();
            m.Offerings = offerings.Select(o => new ResultsExplorerViewModel.OfferingCol(o, subjects.First(s => s.Id == o.SubjectId))).OrderBy(c => c.Subject.Code).ToList();
            var members = await _db.SectionMemberships.AsNoTracking().Where(x => x.SectionId == m.Section.Id && x.EffectiveToUtc == null).Select(x => x.EnrollmentId).ToListAsync();
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => members.Contains(e.Id)).ToListAsync();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => enrollments.Select(e => e.StudentId).Contains(s.Id)).ToListAsync();
            var results = await _db.TermResults.AsNoTracking().Where(r => members.Contains(r.EnrollmentId) && r.TermId == m.Term.Id).ToListAsync();
            var bandIds = results.Where(r => r.ScaleBandId != null).Select(r => r.ScaleBandId!.Value).Distinct().ToList();
            var bands = await _db.ScaleBands.AsNoTracking().Where(b => bandIds.Contains(b.Id)).ToListAsync();
            var yearResults = await _db.YearResults.AsNoTracking().Where(r => members.Contains(r.EnrollmentId) && r.AcademicYearId == yid).ToListAsync();

            var rows = enrollments.Select(e =>
            {
                var by = results.Where(r => r.EnrollmentId == e.Id).ToDictionary(r => r.CurriculumOfferingId, r => (r, bands.FirstOrDefault(b => b.Id == r.ScaleBandId)));
                var w = by.Values.Select(v => (v.r.ScorePercent, Weight: offerings.First(o => o.Id == v.r.CurriculumOfferingId).GpaWeight)).ToList();
                var tw = w.Sum(x => x.Weight);
                decimal? avg = w.Count == 0 ? null : tw > 0 ? TermScoreCalculator.RoundHalfUp(w.Sum(x => x.ScorePercent * x.Weight) / tw) : TermScoreCalculator.RoundHalfUp(w.Average(x => x.ScorePercent));
                var failed = by.Values.Count(v => v.Item2 != null && !v.Item2.IsPassing);
                return (e, s: students.First(s => s.Id == e.StudentId), by, avg, failed);
            }).ToList();
            var ranks = RankCalculator.Rank(rows.Where(r => r.avg != null).Select(r => (r.e.Id, r.avg!.Value))).ToDictionary(r => r.Id, r => r.Rank);
            m.Students = rows.Select(r => new ResultsExplorerViewModel.StudentRow(r.e, r.s, r.by, r.avg, ranks.TryGetValue(r.e.Id, out var rk) ? rk : null, yearResults.FirstOrDefault(y => y.EnrollmentId == r.e.Id), r.failed))
                .OrderBy(r => r.Rank ?? int.MaxValue).ThenBy(r => r.Student.StudentNo).ToList();
            return View(m);
        }

        [HttpPost("results/year")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Results, ActionVerb.Post)]
        public async Task<IActionResult> ComputeYear(int? year, int? section, int? term, int? enrollmentId)
        {
            try
            {
                var yid = year ?? _workingYear.AcademicYearId;
                IEnumerable<Sms.Domain.Students.Enrollment> targets;
                if (enrollmentId != null) targets = new[] { await _db.Enrollments.AsNoTracking().SingleAsync(e => e.Id == enrollmentId) };
                else
                {
                    if (section == null) throw new InvalidOperationException(T("Choose a section.", "اختر شعبة."));
                    var members = await _db.SectionMemberships.AsNoTracking().Where(x => x.SectionId == section && x.EffectiveToUtc == null).Select(x => x.EnrollmentId).ToListAsync();
                    targets = await _db.Enrollments.AsNoTracking().Where(e => members.Contains(e.Id)).ToListAsync();
                }
                var n = 0;
                foreach (var e in targets) { await _grading.ComputeYearResultAsync(e.Id, yid, e.GradeYearProfileId); n++; }
                TempData["Flash"] = T($"Year result computed for {n} student(s) (BR-GRA-006/007).", $"حُسبت نتيجة العام لـ {n} طالباً (BR-GRA-006/007).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Results), new { year, section, term });
        }

        // ================================================================== 8.6 Report card (HTML)

        [HttpGet("reportcard/{enrollmentId:int}")]
        [RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.ReportCard, ActionVerb.View)]
        public async Task<IActionResult> ReportCard(int enrollmentId, int? term = null, bool reprint = false)
        {
            var e = await _db.Enrollments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == enrollmentId);
            if (e == null) return NotFound();
            var terms = await _db.Terms.AsNoTracking().Where(t => t.AcademicYearId == e.AcademicYearId).OrderBy(t => t.SequenceNumber).ToListAsync();
            var t0 = terms.FirstOrDefault(t => t.Id == term) ?? terms.FirstOrDefault();
            if (t0 == null) { TempData["Error"] = T("The year has no terms yet.", "لا فترات لهذا العام بعد."); return RedirectToAction(nameof(Results), new { year = e.AcademicYearId }); }
            var profile = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == e.GradeYearProfileId);
            var offerings = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == profile.Id && o.IsAssessable).ToListAsync();
            var subjects = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var results = await _db.TermResults.AsNoTracking().Where(r => r.EnrollmentId == enrollmentId && r.TermId == t0.Id).ToListAsync();
            var bandIds = results.Where(r => r.ScaleBandId != null).Select(r => r.ScaleBandId!.Value).ToList();
            var bands = await _db.ScaleBands.AsNoTracking().Where(b => bandIds.Contains(b.Id)).ToListAsync();
            var membership = await _db.SectionMemberships.AsNoTracking().Where(x => x.EnrollmentId == enrollmentId && x.EffectiveToUtc == null).FirstOrDefaultAsync();
            var section = membership == null ? null : await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == membership.SectionId);
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _db.CurrentSchoolId);

            var lines = offerings.Select(o => new ReportCardViewModel.Line(subjects.First(s => s.Id == o.SubjectId), o, results.FirstOrDefault(r => r.CurriculumOfferingId == o.Id), bands.FirstOrDefault(b => results.FirstOrDefault(r => r.CurriculumOfferingId == o.Id)?.ScaleBandId == b.Id))).OrderBy(l => l.Subject.Code).ToList();
            var scored = lines.Where(l => l.Result != null).ToList();
            var tw = scored.Sum(l => l.Offering.GpaWeight);
            decimal? avg = scored.Count == 0 ? null : tw > 0 ? TermScoreCalculator.RoundHalfUp(scored.Sum(l => l.Result!.ScorePercent * l.Offering.GpaWeight) / tw) : TermScoreCalculator.RoundHalfUp(scored.Average(l => l.Result!.ScorePercent));
            ScaleBand? avgBand = null;
            if (avg != null && scored.Count > 0)
            {
                // Resolve the average against the first line's scale (report cards typically share one scale per grade).
                var bp = await _db.Blueprints.AsNoTracking().FirstOrDefaultAsync(b => b.CurriculumOfferingId == scored[0].Offering.Id && b.TermId == t0.Id);
                if (bp != null)
                {
                    var scaleBands = await _db.ScaleBands.AsNoTracking().Where(b => b.GradingScaleId == bp.GradingScaleId).ToListAsync();
                    var bid = ScaleBandResolver.Resolve(avg.Value, scaleBands.Select(b => new ScaleBandResolver.Band(b.Id, b.MinPercent, b.MaxPercent)));
                    avgBand = scaleBands.FirstOrDefault(b => b.Id == bid);
                }
            }

            // Section rank for the term (BR-GRA-007 — internal + report card per policy; shown here as the basic subset).
            int? rank = null, rankOf = null;
            if (section != null && avg != null)
            {
                var members = await _db.SectionMemberships.AsNoTracking().Where(x => x.SectionId == section.Id && x.EffectiveToUtc == null).Select(x => x.EnrollmentId).ToListAsync();
                var all = await _db.TermResults.AsNoTracking().Where(r => members.Contains(r.EnrollmentId) && r.TermId == t0.Id).ToListAsync();
                var per = all.GroupBy(r => r.EnrollmentId).Select(g => { var w = g.Select(r => (r.ScorePercent, W: offerings.FirstOrDefault(o => o.Id == r.CurriculumOfferingId)?.GpaWeight ?? 0m)).ToList(); var sw = w.Sum(x => x.W); return (g.Key, Score: sw > 0 ? TermScoreCalculator.RoundHalfUp(w.Sum(x => x.ScorePercent * x.W) / sw) : TermScoreCalculator.RoundHalfUp(w.Average(x => x.ScorePercent))); }).ToList();
                var ranked = RankCalculator.Rank(per);
                rank = ranked.FirstOrDefault(r => r.Id == enrollmentId).Rank; if (rank == 0) rank = null;
                rankOf = per.Count;
            }

            // BR-GRA-004: attendance % from the single BR-ATD-009 computation, over the term's dates.
            var att = await _db.AttendanceDays.AsNoTracking().Where(a => a.EnrollmentId == enrollmentId && a.Date >= t0.StartDate && a.Date <= t0.EndDate).Select(a => a.Status).ToListAsync();
            decimal? attPct = null;
            if (att.Count > 0)
            {
                var exempt = att.Count(s => s == AttendanceStatus.Exempted);
                var absent = att.Count(s => s == AttendanceStatus.AbsentExcused || s == AttendanceStatus.AbsentUnexcused);
                attPct = TermScoreCalculator.RoundHalfUp(AttendancePercentageCalculator.Calculate(att.Count, exempt, absent), 1);
            }

            var m = new ReportCardViewModel
            {
                Student = await _db.Students.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == e.StudentId), Enrollment = e,
                Year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == e.AcademicYearId), Term = t0, Terms = terms,
                Grade = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId), Section = section,
                SchoolNameAr = school?.NameAr ?? "", SchoolNameEn = school?.NameEn ?? "",
                Lines = lines, Average = avg, AverageBand = avgBand, Rank = rank, RankOf = rankOf,
                AttendancePercent = attPct, ScheduledDays = att.Count, AbsentDays = att.Count(s => s == AttendanceStatus.AbsentExcused || s == AttendanceStatus.AbsentUnexcused),
                AllPublished = lines.Count > 0 && lines.All(l => l.Result != null), IsReprint = reprint,
            };
            return View(m);
        }

        // ================================================================== helpers

        private async Task FillPageAsync(GradingPageViewModel m, int? yearId)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            m.Years = years;
            m.Year = years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active) ?? years.FirstOrDefault();
            if (m.Year == null) return;
            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == m.Year.Id && p.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var stages = await _db.Stages.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId).ToListAsync();
            m.Profiles = profiles.Select(p => { var g = grades.First(x => x.Id == p.GradeLevelId); return new GradingPageViewModel.ProfileOption(p.Id, g, stages.FirstOrDefault(s => s.Id == g.StageId) ?? new Sms.Domain.Grades.Stage()); }).OrderBy(p => p.Grade.SequenceOrder).ToList();
            m.Terms = await _db.Terms.AsNoTracking().Where(t => t.AcademicYearId == m.Year.Id).OrderBy(t => t.SequenceNumber).ToListAsync();
        }

        private async Task<MarksheetWorkspaceViewModel?> BuildWorkspaceAsync(int id)
        {
            var sheet = await _db.Marksheets.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (sheet == null) return null;
            var bp = await _db.Blueprints.AsNoTracking().SingleAsync(b => b.Id == sheet.BlueprintId);
            var off = await _db.CurriculumOfferings.AsNoTracking().SingleAsync(o => o.Id == bp.CurriculumOfferingId);
            var profile = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == off.GradeYearProfileId);
            var comps = await _db.BlueprintComponents.AsNoTracking().Where(c => c.BlueprintId == bp.Id).OrderBy(c => c.Id).ToListAsync();
            var bands = await _db.ScaleBands.AsNoTracking().Where(b => b.GradingScaleId == bp.GradingScaleId).OrderBy(b => b.SortOrder).ToListAsync();
            var entries = await _db.MarkEntries.AsNoTracking().Where(e => e.MarksheetId == id).ToListAsync();
            var enrollIds = entries.Select(e => e.EnrollmentId).Distinct().ToList();
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => enrollIds.Contains(e.Id)).ToListAsync();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => enrollments.Select(e => e.StudentId).Contains(s.Id)).ToListAsync();
            var bandArgs = bands.Select(b => new ScaleBandResolver.Band(b.Id, b.MinPercent, b.MaxPercent)).ToList();

            var rows = enrollments.Select(e =>
            {
                var st = students.First(s => s.Id == e.StudentId);
                var es = comps.Select(c => entries.First(x => x.EnrollmentId == e.Id && x.BlueprintComponentId == c.Id)).ToList();
                var resolved = es.All(x => x.Score != null || x.IsAbsent || x.IsExempt);
                decimal? pct = null; ScaleBand? band = null;
                if (resolved && es.Any(x => !x.IsExempt))
                {
                    pct = TermScoreCalculator.RoundHalfUp(TermScoreCalculator.CalculateWeightedPercent(es.Select(x => { var c = comps.First(k => k.Id == x.BlueprintComponentId); return new TermScoreCalculator.ComponentMark(x.Score, c.MaxScore, c.Weight, x.IsAbsent, x.IsExempt); })));
                    var bid = ScaleBandResolver.Resolve(pct.Value, bandArgs); band = bands.FirstOrDefault(b => b.Id == bid);
                }
                return new MarksheetWorkspaceViewModel.StudentRow(e, st, es, pct, band, resolved);
            }).OrderBy(r => IsArabic ? r.Student.FirstNameAr : r.Student.FirstNameEn).ThenBy(r => r.Student.StudentNo).ToList();

            var audit = await _db.AuditEntries.AsNoTracking().Where(a => a.EntityType == nameof(Sms.Domain.Grading.Marksheet) && a.EntityId == id).OrderByDescending(a => a.OccurredAtUtc).Take(50).ToListAsync();
            return new MarksheetWorkspaceViewModel
            {
                Sheet = sheet, Blueprint = bp, Components = comps, Bands = bands,
                Scale = await _db.GradingScales.AsNoTracking().SingleAsync(s => s.Id == bp.GradingScaleId),
                Subject = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == off.SubjectId),
                Grade = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId),
                Term = await _db.Terms.AsNoTracking().SingleAsync(t => t.Id == bp.TermId),
                Section = await _db.Sections.AsNoTracking().SingleAsync(s => s.Id == sheet.SectionId),
                Year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == sheet.AcademicYearId),
                Students = rows,
                AllowedTransitions = Enum.GetValues<MarksheetStatus>().Where(t => MarksheetStatusTransitions.CanTransition(sheet.Status, t) && t != MarksheetStatus.Draft).ToList(),
                HasAnyMark = entries.Any(x => x.Score != null || x.IsAbsent || x.IsExempt),
                Resolved = rows.Count(r => r.Resolved), Total = rows.Count,
                Audit = audit.Select(a => (a.Action.ToString(), a.FieldName, a.OldValue, a.NewValue, a.OccurredAtUtc, a.ActorUserId, a.Reason)).ToList(),
            };
        }

        private async Task<IReadOnlyList<(int Id, string Ar, string En)>> LookupAsync(string category)
        {
            var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == category);
            return cat == null ? Array.Empty<(int, string, string)>() : await _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == cat.Id).OrderBy(v => v.SortOrder).Select(v => new ValueTuple<int, string, string>(v.Id, v.Name.NameAr, v.Name.NameEn)).ToListAsync();
        }
    }
}
