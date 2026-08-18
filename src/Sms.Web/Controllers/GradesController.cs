using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Grades;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/05 §8.1–8.3: ladder builder (stages → grades, promotion
    /// arrows, graduating flags), grade-year profile editor with live seat
    /// math, capacity board (planned vs enrolled). §8.4 active-year change
    /// request (P2) is deferred with the approval workflow wiring.
    /// </summary>
    [Route("grades")]
    public class GradesController : Controller
    {
        private readonly IGradeStructureAdmin _grades;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;

        public GradesController(IGradeStructureAdmin grades, AppDbContext db, IWorkingYearContext workingYear)
        {
            _grades = grades;
            _db = db;
            _workingYear = workingYear;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        public async Task<IActionResult> Index(int? year = null)
        {
            return View(await BuildAsync(year));
        }

        [HttpPost("stage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineStage(GradeLadderViewModel form, int? year)
        {
            try
            {
                Require(form.StageNameAr, T("Stage name (Arabic)", "اسم المرحلة (عربي)"));
                Require(form.StageNameEn, T("Stage name (English)", "اسم المرحلة (إنجليزي)"));
                await _grades.DefineStageAsync(form.StageNameAr!, form.StageNameEn!, form.StageOrder ?? 1, form.StageGender);
                TempData["Flash"] = T("Stage created.", "تم إنشاء المرحلة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("grade")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineGrade(GradeLadderViewModel form, int? year)
        {
            try
            {
                if (form.GradeStageId == null) throw new InvalidOperationException(T("Choose a stage.", "اختر مرحلة."));
                Require(form.GradeCode, T("Code", "الرمز"));
                Require(form.GradeNameAr, T("Grade name (Arabic)", "اسم الصف (عربي)"));
                Require(form.GradeNameEn, T("Grade name (English)", "اسم الصف (إنجليزي)"));
                await _grades.DefineGradeLevelAsync(form.GradeStageId.Value, form.GradeCode!.Trim(), form.GradeNameAr!, form.GradeNameEn!, form.GradeOrder ?? 1, form.PromotionTargetId, form.IsGraduating);
                TempData["Flash"] = T("Grade created.", "تم إنشاء الصف.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("grade/{id:int}/path")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPath(int id, int? target, bool graduating, int? year)
        {
            try
            {
                await _grades.SetPromotionPathAsync(id, target, graduating);
                TempData["Flash"] = T("Promotion path updated.", "تم تحديث مسار الترفيع.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpPost("profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineProfile(GradeLadderViewModel form, int year)
        {
            try
            {
                if (form.ProfileGradeId == null) throw new InvalidOperationException(T("Choose a grade.", "اختر صفاً."));
                if (form.MinAge != null && form.MaxAge != null && form.MinAge >= form.MaxAge) throw new InvalidOperationException(T("Minimum age must be below maximum age (§9).", "الحد الأدنى للعمر يجب أن يقل عن الأقصى (§9)."));
                await _grades.DefineGradeYearProfileAsync(form.ProfileGradeId.Value, year, form.ProfileGender, form.TargetSections ?? 1, form.TargetSectionSize ?? 25,
                    form.CurriculumId, form.MinAge, form.MaxAge, form.AgeCutoff);
                TempData["Flash"] = T("Grade-year profile saved.", "تم حفظ ملف الصف السنوي.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { year });
        }

        private async Task<GradeLadderViewModel> BuildAsync(int? yearId)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var year = years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active) ?? years.FirstOrDefault();
            var stages = await _db.Stages.AsNoTracking().OrderBy(s => s.SequenceOrder).ToListAsync();
            var grades = await _db.GradeLevels.AsNoTracking().OrderBy(g => g.SequenceOrder).ToListAsync();
            var profiles = year == null ? new() : await _db.GradeYearProfiles.AsNoTracking().Where(p => p.AcademicYearId == year.Id).ToListAsync();
            var sections = year == null ? new() : await _db.Sections.AsNoTracking().Where(s => s.AcademicYearId == year.Id).ToListAsync();
            var enrolled = year == null
                ? new System.Collections.Generic.Dictionary<int, int>()
                : await _db.Enrollments.AsNoTracking().Where(e => e.AcademicYearId == year.Id && e.ExitDate == null).GroupBy(e => e.GradeYearProfileId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var curriculumCat = await _db.LookupCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Code == "Curriculum");
            var curricula = curriculumCat == null ? new() : await _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == curriculumCat.Id).OrderBy(v => v.SortOrder).ToListAsync();

            return new GradeLadderViewModel
            {
                Years = years,
                Year = year,
                AllGrades = grades,
                Curricula = curricula.Select(c => (c.Id, c.Code, c.Name.NameAr, c.Name.NameEn)).ToList(),
                Stages = stages.Select(s => new GradeLadderViewModel.StageRow(s, grades.Where(g => g.StageId == s.Id).Select(g =>
                {
                    var p = profiles.FirstOrDefault(x => x.GradeLevelId == g.Id);
                    return new GradeLadderViewModel.GradeRow(g, grades.FirstOrDefault(x => x.Id == g.PromotionTargetGradeLevelId), p,
                        p == null ? 0 : sections.Count(x => x.GradeYearProfileId == p.Id), p == null ? 0 : (enrolled.TryGetValue(p.Id, out var n) ? n : 0));
                }).ToList())).ToList(),
                StageOrder = stages.Count + 1,
                GradeOrder = grades.Count + 1,
                TargetSections = 1,
                TargetSectionSize = 25,
            };
        }

        private static void Require(string? v, string f)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{f} is required.", $"الحقل {f} مطلوب."));
        }
    }
}
