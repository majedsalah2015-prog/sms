using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Sections;
using Sms.Domain.Grades;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/06 §8.1, §8.2, §8.4: section list per grade/year with
    /// capacity meters, section detail (roster, homeroom history, assign
    /// student, transfer dialog, close). §8.3 drag-drop assignment board /
    /// auto-distribute and §8.5 merge wizard are E-801 rollover-cockpit
    /// screens (SectionDistributor engine already exists there).
    /// Homeroom teachers are UserAccount ids on HomeroomAssignment (the
    /// documented identity-bridge inconsistency): the picker lists
    /// TeacherProfiles and can only assign those whose Employee has a linked
    /// user account.
    /// </summary>
    [Route("sections")]
    public class SectionsController : Controller
    {
        private readonly ISectionAdmin _sections;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly IClock _clock;

        public SectionsController(ISectionAdmin sections, AppDbContext db, IWorkingYearContext workingYear, IClock clock)
        {
            _sections = sections;
            _db = db;
            _workingYear = workingYear;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var selected = years.FirstOrDefault(y => y.Id == (year ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active) ?? years.FirstOrDefault();
            var model = new SectionListViewModel { Years = years, Year = selected, Capacity = 25 };
            if (selected != null)
            {
                var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => p.AcademicYearId == selected.Id).ToListAsync();
                var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
                var sections = await _db.Sections.AsNoTracking().Where(s => s.AcademicYearId == selected.Id).OrderBy(s => s.NameEn).ToListAsync();
                var members = await _db.SectionMemberships.AsNoTracking().Where(m => m.AcademicYearId == selected.Id && m.EffectiveToUtc == null).GroupBy(m => m.SectionId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
                var homerooms = await _db.HomeroomAssignments.AsNoTracking().Where(h => h.AcademicYearId == selected.Id && h.EffectiveToUtc == null).ToListAsync();
                var teacherNames = await TeacherNamesByUserAsync();
                var rooms = await _db.Rooms.AsNoTracking().ToDictionaryAsync(r => r.Id, r => IsArabic ? r.Name.NameAr : r.Name.NameEn);

                model.Profiles = profiles.Where(p => p.IsActive).Select(p => { var g = grades.First(x => x.Id == p.GradeLevelId); return (p.Id, g.Name.NameAr, g.Name.NameEn, p.TargetSections, p.TargetSectionSize); }).OrderBy(x => x.NameEn).ToList();
                model.Rows = sections.Select(s =>
                {
                    var p = profiles.First(x => x.Id == s.GradeYearProfileId);
                    var g = grades.First(x => x.Id == p.GradeLevelId);
                    var hr = homerooms.FirstOrDefault(h => h.SectionId == s.Id);
                    return new SectionListViewModel.Row(s, g, p, members.TryGetValue(s.Id, out var n) ? n : 0,
                        hr == null ? null : (teacherNames.TryGetValue(hr.TeacherUserId, out var tn) ? tn : $"#{hr.TeacherUserId}"),
                        s.DefaultClassroomId != null && rooms.TryGetValue(s.DefaultClassroomId.Value, out var rn) ? rn : null);
                }).OrderBy(r => r.Grade.SequenceOrder).ThenBy(r => r.Section.NameEn).ToList();
            }

            return View(model);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Create)]
        public async Task<IActionResult> Define(SectionListViewModel form, int? year)
        {
            try
            {
                if (form.GradeYearProfileId == null) throw new InvalidOperationException(T("Choose a grade.", "اختر صفاً."));
                Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)"));
                Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                var s = await _sections.DefineSectionAsync(form.GradeYearProfileId.Value, form.NameAr!, form.NameEn!, form.Capacity ?? 25, form.GenderPolicy);
                TempData["Flash"] = T("Section created.", "تم إنشاء الشعبة.");
                return RedirectToAction(nameof(Details), new { id = s.Id });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { year });
        }

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.View)]
        public async Task<IActionResult> Details(int id)
        {
            var model = await BuildDetailAsync(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("{id:int}/homeroom")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit)]
        public async Task<IActionResult> Homeroom(int id, int? teacherUserId, DateTime? effectiveFrom)
        {
            try
            {
                if (teacherUserId == null) throw new InvalidOperationException(T("Choose a teacher with a linked user account.", "اختر معلماً له حساب مستخدم مرتبط."));
                await _sections.AssignHomeroomTeacherAsync(id, teacherUserId.Value, DateTime.SpecifyKind(effectiveFrom ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Homeroom teacher assigned; the previous assignment was closed (BR-SCN-004).", "تم تعيين رائد الفصل وإغلاق التعيين السابق (BR-SCN-004).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/assign")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit)]
        public async Task<IActionResult> Assign(int id, int? enrollmentId, DateTime? effectiveFrom)
        {
            try
            {
                if (enrollmentId == null) throw new InvalidOperationException(T("Choose a student.", "اختر طالباً."));
                await _sections.AssignMembershipAsync(id, enrollmentId.Value, DateTime.SpecifyKind(effectiveFrom ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Student assigned.", "تم إسناد الطالب.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost("{id:int}/transfer")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, ActionVerb.Edit)]
        public async Task<IActionResult> Transfer(int id, int enrollmentId, int? targetSectionId, string? reasonCode, DateTime? effectiveDate)
        {
            try
            {
                if (targetSectionId == null) throw new InvalidOperationException(T("Choose a target section.", "اختر الشعبة المستهدفة."));
                Require(reasonCode, T("Reason code", "رمز السبب"));
                await _sections.TransferMembershipAsync(enrollmentId, targetSectionId.Value, reasonCode!, effectiveDate ?? _clock.UtcNow.Date);
                TempData["Flash"] = T("Student transferred; history kept (BR-SCN-005/006).", "تم نقل الطالب مع حفظ السجل (BR-SCN-005/006).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        // --- Edit / delete ---------------------------------------------------------

        [HttpGet("{id:int}/edit")]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Edit)]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await BuildEditAsync(id);
            if (model == null) return NotFound();
            model.NameAr = model.Section.NameAr;
            model.NameEn = model.Section.NameEn;
            model.Capacity = model.Section.Capacity;
            model.GenderPolicy = model.Section.GenderPolicy;
            model.DefaultClassroomId = model.Section.DefaultClassroomId;
            return View(model);
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Edit)]
        public async Task<IActionResult> Edit(int id, SectionEditViewModel form)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (section == null) return NotFound();
            try
            {
                Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)"));
                Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                await _sections.UpdateSectionAsync(id, form.NameAr!.Trim(), form.NameEn!.Trim(), form.Capacity ?? section.Capacity, form.GenderPolicy, form.DefaultClassroomId);
                TempData["Flash"] = T("Section updated.", "تم تحديث الشعبة.");
                return RedirectToAction(nameof(Index), new { year = section.AcademicYearId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var model = (await BuildEditAsync(id))!;
                model.NameAr = form.NameAr; model.NameEn = form.NameEn; model.Capacity = form.Capacity; model.GenderPolicy = form.GenderPolicy; model.DefaultClassroomId = form.DefaultClassroomId;
                return View(model);
            }
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Deactivate)]
        public async Task<IActionResult> Delete(int id)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            try
            {
                await _sections.DeleteSectionAsync(id);
                TempData["Flash"] = T("Section deleted.", "تم حذف الشعبة.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { year = section?.AcademicYearId });
        }

        private async Task<SectionEditViewModel?> BuildEditAsync(int id)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (section == null) return null;
            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleAsync(p => p.Id == section.GradeYearProfileId);
            var grade = await _db.GradeLevels.AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId);
            var rooms = await _db.Rooms.AsNoTracking().ToListAsync();
            return new SectionEditViewModel
            {
                Id = id, Section = section,
                GradeLabelAr = $"{grade.Code} {grade.Name.NameAr}", GradeLabelEn = $"{grade.Code} {grade.Name.NameEn}",
                PlanSectionSize = profile.TargetSectionSize, GradeGender = profile.GenderPolicy,
                CurrentMembers = await _db.SectionMemberships.CountAsync(m => m.SectionId == id && m.EffectiveToUtc == null),
                Rooms = rooms.Select(r => (r.Id, r.Name.NameAr, r.Name.NameEn)).OrderBy(r => r.NameEn).ToList(),
            };
        }

        [HttpPost("{id:int}/close")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, ActionVerb.Approve)]
        public async Task<IActionResult> Close(int id)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            try
            {
                await _sections.CloseSectionAsync(id);
                TempData["Flash"] = T("Section closed.", "تم إغلاق الشعبة.");
                return RedirectToAction(nameof(Index), new { year = section?.AcademicYearId });
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<SectionDetailViewModel?> BuildDetailAsync(int id)
        {
            var section = await _db.Sections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
            if (section == null) return null;
            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleAsync(p => p.Id == section.GradeYearProfileId);
            var grade = await _db.GradeLevels.AsNoTracking().SingleAsync(g => g.Id == profile.GradeLevelId);
            var year = await _db.AcademicYears.AsNoTracking().SingleAsync(y => y.Id == section.AcademicYearId);
            var memberships = await _db.SectionMemberships.AsNoTracking().Where(m => m.SectionId == id).OrderByDescending(m => m.EffectiveFromUtc).ToListAsync();
            var enrollmentIds = memberships.Select(m => m.EnrollmentId).Distinct().ToList();
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => enrollmentIds.Contains(e.Id)).ToListAsync();
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
            SectionDetailViewModel.MemberRow Row(SectionMembership m)
            {
                var e = enrollments.First(x => x.Id == m.EnrollmentId);
                students.TryGetValue(e.StudentId, out var st);
                return new SectionDetailViewModel.MemberRow(m, st?.StudentNo ?? "?", st == null ? "?" : $"{st.FirstNameAr} {st.FatherNameAr} {st.FamilyNameAr}", st == null ? "?" : $"{st.FirstNameEn} {st.FatherNameEn} {st.FamilyNameEn}", e.Id);
            }

            var homerooms = await _db.HomeroomAssignments.AsNoTracking().Where(h => h.SectionId == id).OrderByDescending(h => h.EffectiveFromUtc).ToListAsync();
            var teacherNames = await TeacherNamesByUserAsync();
            var teacherOptions = await TeacherOptionsAsync();

            // Unassigned = enrollments of this grade-year profile with no current membership anywhere.
            var assignedNow = await _db.SectionMemberships.AsNoTracking().Where(m => m.AcademicYearId == section.AcademicYearId && m.EffectiveToUtc == null).Select(m => m.EnrollmentId).ToListAsync();
            var candidates = await _db.Enrollments.AsNoTracking().Where(e => e.GradeYearProfileId == profile.Id && e.ExitDate == null && !assignedNow.Contains(e.Id)).ToListAsync();
            var candStudents = await _db.Students.AsNoTracking().Where(s => candidates.Select(c => c.StudentId).Contains(s.Id)).ToDictionaryAsync(s => s.Id);
            var siblings = await _db.Sections.AsNoTracking().Where(s => s.GradeYearProfileId == profile.Id && s.Id != id && s.Status == SectionStatus.Active).ToListAsync();
            var room = section.DefaultClassroomId == null ? null : await _db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == section.DefaultClassroomId);

            return new SectionDetailViewModel
            {
                Section = section, Grade = grade, Year = year,
                Members = memberships.Where(m => m.EffectiveToUtc == null).Select(Row).OrderBy(r => r.NameEn).ToList(),
                PastMembers = memberships.Where(m => m.EffectiveToUtc != null).Select(Row).ToList(),
                Homerooms = homerooms.Select(h => new SectionDetailViewModel.HomeroomRow(h, teacherNames.TryGetValue(h.TeacherUserId, out var n) ? n : $"#{h.TeacherUserId}")).ToList(),
                Teachers = teacherOptions,
                Unassigned = candidates.Select(c => { candStudents.TryGetValue(c.StudentId, out var st); return new SectionDetailViewModel.EnrollmentOption(c.Id, st?.StudentNo ?? "?", st == null ? "?" : $"{st.FirstNameAr} {st.FamilyNameAr}", st == null ? "?" : $"{st.FirstNameEn} {st.FamilyNameEn}"); }).ToList(),
                SiblingSections = siblings,
                RoomName = room == null ? null : (IsArabic ? room.Name.NameAr : room.Name.NameEn),
            };
        }

        private async Task<Dictionary<int, string>> TeacherNamesByUserAsync()
        {
            var employees = await _db.Employees.AsNoTracking().Where(e => e.UserAccountId != null).ToListAsync();
            return employees.GroupBy(e => e.UserAccountId!.Value).ToDictionary(g => g.Key, g => { var e = g.First(); return IsArabic ? $"{e.FirstNameAr} {e.FamilyNameAr}" : $"{e.FirstNameEn} {e.FamilyNameEn}"; });
        }

        private async Task<IReadOnlyList<SectionDetailViewModel.TeacherOption>> TeacherOptionsAsync()
        {
            var profiles = await _db.TeacherProfiles.AsNoTracking().ToListAsync();
            var ids = profiles.Select(p => p.EmployeeId).ToList();
            var employees = await _db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync();
            return employees.Select(e => new SectionDetailViewModel.TeacherOption(e.UserAccountId, $"{e.FirstNameAr} {e.FamilyNameAr}", $"{e.FirstNameEn} {e.FamilyNameEn}")).OrderBy(t => t.NameEn).ToList();
        }

        private static void Require(string? v, string f)
        {
            if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{f} is required.", $"الحقل {f} مطلوب."));
        }
    }
}
