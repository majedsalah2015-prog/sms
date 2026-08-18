using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Subjects;
using Sms.Domain.Schools;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/07 §8.1–8.4: subject catalog (department filter, usage
    /// indicators), department manager, qualification matrix (teacher ×
    /// subject with stage restriction; gaps highlighted), curriculum plan
    /// editor per grade-year (offerings grid, live period total vs available
    /// slots BR-SUB-005, copy-from-previous-year). §8.5 plan change request
    /// (P2) is deferred with the approval workflow wiring. Teacher identity =
    /// UserAccount id (documented bridge gap) — pickers list TeacherProfiles
    /// whose Employee has a linked account.
    /// </summary>
    [Route("subjects")]
    public class SubjectsController : Controller
    {
        private readonly ISubjectAdmin _subjects;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;

        public SubjectsController(ISubjectAdmin subjects, AppDbContext db, IWorkingYearContext workingYear)
        {
            _subjects = subjects;
            _db = db;
            _workingYear = workingYear;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        public async Task<IActionResult> Index(string? tab = null, int? department = null)
        {
            var subjects = await _db.Subjects.AsNoTracking().OrderBy(s => s.Code).ToListAsync();
            var departments = await _db.Departments.AsNoTracking().OrderBy(d => d.Name.NameEn).ToListAsync();
            var offerings = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.EffectiveToUtc == null).GroupBy(o => o.SubjectId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var quals = await _db.TeacherSubjectQualifications.AsNoTracking().ToListAsync();
            var stages = await _db.Stages.AsNoTracking().OrderBy(s => s.SequenceOrder).ToListAsync();
            var teachers = await TeacherOptionsAsync();

            var matrix = quals.GroupBy(q => q.TeacherUserId).ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<int, IReadOnlyList<string>>)g.GroupBy(q => q.SubjectId).ToDictionary(
                    x => x.Key,
                    x => (IReadOnlyList<string>)x.Select(q => q.StageId == null ? T("all stages", "كل المراحل") : (stages.FirstOrDefault(s => s.Id == q.StageId) is { } st ? (IsArabic ? st.Name.NameAr : st.Name.NameEn) : "?")).ToList()));

            return View(new SubjectCatalogViewModel
            {
                ActiveTab = tab ?? "catalog",
                DepartmentFilter = department,
                Departments = departments,
                Stages = stages,
                Teachers = teachers,
                Matrix = matrix,
                Subjects = subjects.Where(s => department == null || s.DepartmentId == department)
                    .Select(s => new SubjectCatalogViewModel.SubjectRow(s, departments.FirstOrDefault(d => d.Id == s.DepartmentId),
                        offerings.TryGetValue(s.Id, out var n) ? n : 0, quals.Where(q => q.SubjectId == s.Id).Select(q => q.TeacherUserId).Distinct().Count())).ToList(),
            });
        }

        [HttpPost("subject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineSubject(SubjectCatalogViewModel form)
        {
            try
            {
                Require(form.Code, T("Code", "الرمز")); Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)")); Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                await _subjects.DefineSubjectAsync(form.Code!.Trim().ToUpperInvariant(), form.NameAr!, form.NameEn!, form.Category ?? "core", form.DepartmentId);
                TempData["Flash"] = T("Subject created.", "تم إنشاء المادة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { tab = "catalog" });
        }

        [HttpPost("department")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineDepartment(SubjectCatalogViewModel form)
        {
            try
            {
                Require(form.DeptNameAr, T("Name (Arabic)", "الاسم (عربي)")); Require(form.DeptNameEn, T("Name (English)", "الاسم (إنجليزي)"));
                await _subjects.DefineDepartmentAsync(form.DeptNameAr!, form.DeptNameEn!, form.HeadTeacherUserId);
                TempData["Flash"] = T("Department created.", "تم إنشاء القسم.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { tab = "departments" });
        }

        [HttpPost("qualification")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineQualification(SubjectCatalogViewModel form)
        {
            try
            {
                if (form.QTeacherUserId == null || form.QSubjectId == null) throw new InvalidOperationException(T("Choose a teacher and a subject.", "اختر معلماً ومادة."));
                await _subjects.DefineQualificationAsync(form.QTeacherUserId.Value, form.QSubjectId.Value, form.QStageId, form.QSource);
                TempData["Flash"] = T("Qualification recorded (BR-SUB-006).", "تم تسجيل التأهيل (BR-SUB-006).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { tab = "matrix" });
        }

        // ------------------------------------------------------------------ Curriculum plan

        [HttpGet("plan")]
        public async Task<IActionResult> Plan(int? year = null, int? profile = null, int? slots = null)
        {
            var model = await BuildPlanAsync(year, profile, slots);
            return View(model);
        }

        [HttpPost("plan/offering")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOffering(CurriculumPlanViewModel form, int year, int profile, int? slots)
        {
            try
            {
                if (form.SubjectId == null) throw new InvalidOperationException(T("Choose a subject.", "اختر مادة."));
                var yr = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == year);
                await _subjects.DefineOfferingAsync(profile, form.SubjectId.Value, form.WeeklyPeriods ?? 1, form.IsAssessable, form.GpaWeight ?? 0m, form.IsElective, string.IsNullOrWhiteSpace(form.ElectiveGroupTag) ? null : form.ElectiveGroupTag.Trim(), yr.StartDate);
                TempData["Flash"] = T("Offering added.", "تمت إضافة المادة للخطة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Plan), new { year, profile, slots });
        }

        [HttpPost("plan/offering/{id:int}/end")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndOffering(int id, int year, int profile, DateTime? effectiveTo, int? slots)
        {
            try
            {
                var yr = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == year);
                await _subjects.EndDateOfferingAsync(id, effectiveTo ?? yr.EndDate);
                TempData["Flash"] = T("Offering end-dated (BR-SUB-004: never removed).", "تم إنهاء المادة بتاريخ (BR-SUB-004: لا تُحذف).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Plan), new { year, profile, slots });
        }

        [HttpPost("plan/copy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CopyPlan(int year, int profile, int sourceProfile, int? slots)
        {
            var copied = 0;
            try
            {
                var yr = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == year);
                var source = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == sourceProfile && o.EffectiveToUtc == null).ToListAsync();
                var existing = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == profile && o.EffectiveToUtc == null).Select(o => o.SubjectId).ToListAsync();
                foreach (var o in source.Where(o => !existing.Contains(o.SubjectId)))
                {
                    await _subjects.DefineOfferingAsync(profile, o.SubjectId, o.WeeklyPeriods, o.IsAssessable, o.GpaWeight, o.IsElective, o.ElectiveGroupTag, yr.StartDate);
                    copied++;
                }

                TempData["Flash"] = T($"{copied} offering(s) copied.", $"تم نسخ {copied} مادة/مواد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message + (copied > 0 ? T($" ({copied} copied before the error.)", $" (نُسخت {copied} قبل الخطأ.)") : ""); }
            return RedirectToAction(nameof(Plan), new { year, profile, slots });
        }

        private async Task<CurriculumPlanViewModel> BuildPlanAsync(int? yearId, int? profileId, int? slots)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var year = years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active) ?? years.FirstOrDefault();
            var m = new CurriculumPlanViewModel { Years = years, Year = year, AvailableSlots = slots ?? 35, Subjects = await _db.Subjects.AsNoTracking().OrderBy(s => s.Code).ToListAsync() };
            if (year == null) return m;

            var grades = await _db.GradeLevels.AsNoTracking().ToListAsync();
            var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => p.AcademicYearId == year.Id).ToListAsync();
            m.Profiles = profiles.Select(p => new CurriculumPlanViewModel.ProfileOption(p.Id, grades.First(g => g.Id == p.GradeLevelId))).OrderBy(p => p.Grade.SequenceOrder).ToList();
            m.Profile = m.Profiles.FirstOrDefault(p => p.ProfileId == profileId) ?? m.Profiles.FirstOrDefault();
            if (m.Profile == null) return m;

            var offerings = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == m.Profile.ProfileId).OrderBy(o => o.EffectiveToUtc != null).ThenBy(o => o.SubjectId).ToListAsync();
            m.Offerings = offerings.Select(o => new CurriculumPlanViewModel.OfferingRow(o, m.Subjects.First(s => s.Id == o.SubjectId))).ToList();
            m.TotalPeriods = CurriculumPlanValidator.TotalWeeklyPeriods(offerings.Where(o => o.EffectiveToUtc == null).Select(o => o.WeeklyPeriods));

            // Copy-from-previous-year: the same grade's profile in the year that ends right before this one.
            var prevYear = years.Where(y => y.EndDate < year.StartDate).OrderByDescending(y => y.EndDate).FirstOrDefault();
            if (prevYear != null)
            {
                var prevProfile = await _db.GradeYearProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AcademicYearId == prevYear.Id && p.GradeLevelId == m.Profile.Grade.Id);
                if (prevProfile != null && await _db.CurriculumOfferings.AnyAsync(o => o.GradeYearProfileId == prevProfile.Id))
                {
                    m.PreviousYearProfileId = prevProfile.Id;
                    m.PreviousYearLabel = IsArabic ? prevYear.LabelAr : prevYear.LabelEn;
                }
            }

            return m;
        }

        private async Task<IReadOnlyList<SubjectCatalogViewModel.TeacherOption>> TeacherOptionsAsync()
        {
            var ids = await _db.TeacherProfiles.AsNoTracking().Select(p => p.EmployeeId).ToListAsync();
            var employees = await _db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync();
            return employees.Select(e => new SubjectCatalogViewModel.TeacherOption(e.UserAccountId, $"{e.FirstNameAr} {e.FamilyNameAr}", $"{e.FirstNameEn} {e.FamilyNameEn}")).OrderBy(t => t.NameEn).ToList();
        }

        private static void Require(string? v, string f)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{f} is required.", $"الحقل {f} مطلوب."));
        }
    }
}
