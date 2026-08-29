using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Teachers;
using Sms.Domain.Employees;
using Sms.Domain.Schools;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/13 §8 — E-203 (Teachers, academic overlay): 8.1 teacher
    /// directory (subjects, load %, homeroom), 8.2 assignment matrix
    /// (offerings × sections per grade, teacher pickers, live load meters,
    /// qualification warning, copy-from-last-year), 8.3 load board.
    /// Designation itself lives on the Employee file's Teaching tab (and the
    /// directory's quick form). Deferred: availability editor (Module 15
    /// hard constraints), reassignment impact panel + P2 (BR-TCH-007 chain),
    /// teacher workspace "My classes" (needs timetable sessions + the
    /// teacher-identity bridge — Employee.UserAccountId is still nullable and
    /// unprovisioned, see the E-203/E-304/E-401 identity-bridging note).
    /// The qualification warning reads Module 07's matrix via
    /// Employee.UserAccountId → TeacherSubjectQualification.TeacherUserId —
    /// that same bridge — and says "unknown" when the employee has no account.
    /// </summary>
    [Route("teachers")]
    public class TeachersController : Controller
    {
        private readonly ITeacherAdmin _teachers;
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;
        private readonly IClock _clock;

        public TeachersController(ITeacherAdmin teachers, AppDbContext db, IWorkingYearContext workingYear, IClock clock)
        {
            _teachers = teachers;
            _db = db;
            _workingYear = workingYear;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.1 Directory

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Teachers_, ActionVerb.View)]
        public async Task<IActionResult> Index(int? year = null, string? q = null, int? subject = null)
        {
            var (years, yr) = await YearsAsync(year);
            var profiles = await _db.TeacherProfiles.AsNoTracking().ToListAsync();
            var total = profiles.Count;
            var empIds = profiles.Select(p => p.EmployeeId).ToList();
            var employeeQuery = _db.Employees.AsNoTracking().Where(e => empIds.Contains(e.Id));
            if (!string.IsNullOrWhiteSpace(q))
            {
                // A directory of three hundred names is read by number as often as by name, and the
                // mobile is searched as well as filed — same fields the employee directory answers on.
                var t = q.Trim();
                employeeQuery = employeeQuery.Where(e => e.EmployeeNo.Contains(t) || e.FirstNameAr.Contains(t) || e.FatherNameAr.Contains(t) || e.FamilyNameAr.Contains(t) || e.FirstNameEn.Contains(t) || e.FamilyNameEn.Contains(t) || e.FatherNameEn.Contains(t) || (e.Mobile != null && e.Mobile.Contains(t)));
            }
            var employees = await employeeQuery.ToListAsync();
            var now = _clock.UtcNow;
            var activeContractEmp = await _db.Contracts.AsNoTracking().Where(c => empIds.Contains(c.EmployeeId) && c.Status == ContractStatus.Active && c.StartDate <= now && c.EndDate >= now).Select(c => c.EmployeeId).Distinct().ToListAsync();
            var tas = yr == null ? new List<TeacherAssignment>() : await _db.TeacherAssignments.AsNoTracking().Where(a => a.AcademicYearId == yr.Id && a.EffectiveToUtc == null).ToListAsync();
            var offs = await _db.CurriculumOfferings.AsNoTracking().Where(o => tas.Select(a => a.CurriculumOfferingId).Contains(o.Id)).ToListAsync();
            var subs = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => offs.Select(o => o.SubjectId).Contains(s.Id)).ToListAsync();
            var homerooms = yr == null ? new List<Sms.Domain.Sections.HomeroomAssignment>() : await _db.HomeroomAssignments.AsNoTracking().Where(h => h.AcademicYearId == yr.Id && h.EffectiveToUtc == null).ToListAsync();
            var hrSections = await _db.Sections.AsNoTracking().Where(s => homerooms.Select(h => h.SectionId).Contains(s.Id)).ToListAsync();

            // "Who teaches Maths this year?" is the other half of the question the search box answers,
            // and the year's assignments already say it. The picker offers the subjects actually
            // taught; a subject retired mid-year stays listed while it is the one being filtered on,
            // so the select cannot silently reset itself while the list keeps filtering.
            var subjectOptions = subs.Where(s => s.IsActive || s.Id == subject).OrderBy(s => IsArabic ? s.Name.NameAr : s.Name.NameEn).ToList();
            if (subject != null)
            {
                var teaching = tas.Where(a => offs.Any(o => o.Id == a.CurriculumOfferingId && o.SubjectId == subject)).Select(a => a.TeacherProfileId).Distinct().ToList();
                profiles = profiles.Where(p => teaching.Contains(p.Id)).ToList();
            }

            var rows = profiles.Select(p =>
            {
                var e = employees.FirstOrDefault(x => x.Id == p.EmployeeId); if (e == null) return null;
                var mine = tas.Where(a => a.TeacherProfileId == p.Id).ToList();
                var myOffs = mine.Select(a => offs.FirstOrDefault(o => o.Id == a.CurriculumOfferingId)).Where(o => o != null).Select(o => o!).ToList();
                var subjects = myOffs.Select(o => subs.FirstOrDefault(s => s.Id == o.SubjectId)).Where(s => s != null).Select(s => IsArabic ? s!.Name.NameAr : s!.Name.NameEn).Distinct().OrderBy(x => x).ToList();
                var hr = e.UserAccountId == null ? null : homerooms.FirstOrDefault(h => h.TeacherUserId == e.UserAccountId);
                var hrSec = hr == null ? null : hrSections.FirstOrDefault(s => s.Id == hr.SectionId);
                return new TeacherDirectoryViewModel.Row(p, e, TeacherLoadCalculator.CurrentLoad(myOffs.Select(o => o.WeeklyPeriods).ToArray()), subjects, mine.Select(a => a.SectionId).Distinct().Count(), hrSec == null ? null : (IsArabic ? hrSec.NameAr : hrSec.NameEn), activeContractEmp.Contains(e.Id));
            }).Where(r => r != null).Select(r => r!).OrderBy(r => r.Employee.EmployeeNo).ToList();

            var designatable = await _db.Employees.AsNoTracking().Where(e => !empIds.Contains(e.Id) && e.Status == EmployeeStatus.Active).OrderBy(e => e.EmployeeNo).Take(300).ToListAsync();
            return View(new TeacherDirectoryViewModel { Rows = rows, Designatable = designatable, Year = yr, Years = years, Query = q, SubjectId = subject, Subjects = subjectOptions, Total = total });
        }

        [HttpPost("designate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Teachers_, ActionVerb.Edit)]
        public async Task<IActionResult> Designate(int? employeeId, int? maxWeeklyPeriods, int? year, string? q = null, int? subject = null)
        {
            try
            {
                if (employeeId == null || maxWeeklyPeriods == null || maxWeeklyPeriods <= 0) throw new InvalidOperationException(T("Choose an employee and a positive max weekly load.", "اختر موظفاً ونصاباً أسبوعياً موجباً."));
                if (await _db.TeacherProfiles.AnyAsync(p => p.EmployeeId == employeeId)) throw new InvalidOperationException(T("Already a teacher.", "معلم مسبقاً."));
                await _teachers.DesignateTeacherAsync(employeeId.Value, maxWeeklyPeriods.Value);
                TempData["Flash"] = T("Teacher designated (BR-TCH-001).", "عُيِّن المعلم (BR-TCH-001).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year, q, subject });
        }

        [HttpPost("{teacherProfileId:int}/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Teachers_, ActionVerb.Deactivate)]
        public async Task<IActionResult> RemoveDesignation(int teacherProfileId, int? year, string? q = null, int? subject = null)
        {
            try
            {
                await _teachers.RemoveDesignationAsync(teacherProfileId);
                TempData["Flash"] = T("Teacher designation removed (the employee record is kept).", "أُزيل تعيين المعلم (سجل الموظف محفوظ).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { year, q, subject });
        }

        // ================================================================== 8.2 Assignment matrix

        [HttpGet("matrix")]
        [RequirePermission(ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Assignments, ActionVerb.View)]
        public async Task<IActionResult> Matrix(int? year = null, int? profile = null)
        {
            var m = await BuildMatrixAsync(year, profile);
            return View(m);
        }

        [HttpPost("matrix/assign")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Assignments, ActionVerb.Create)]
        public async Task<IActionResult> Assign(int? year, int? profile, int? teacherProfileId, int? offeringId, int? sectionId, TeacherRole role, bool overrideLoad, DateTime? effectiveFrom)
        {
            try
            {
                if (teacherProfileId == null || offeringId == null || sectionId == null) throw new InvalidOperationException(T("Choose a teacher, offering and section.", "اختر معلماً ومادة وشعبة."));
                await _teachers.AssignAsync(teacherProfileId.Value, offeringId.Value, sectionId.Value, role, DateTime.SpecifyKind(effectiveFrom ?? _clock.UtcNow.Date, DateTimeKind.Utc), overrideLoad);
                TempData["Flash"] = T("Teacher assigned.", "تم إسناد المعلم.") + (overrideLoad ? " " + T("(load override logged, BR-TCH-004)", "(تجاوز النصاب مسجَّل، BR-TCH-004)") : "");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Matrix), new { year, profile });
        }

        [HttpPost("matrix/{assignmentId:int}/end")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Assignments, ActionVerb.Deactivate)]
        public async Task<IActionResult> EndAssignment(int assignmentId, int? year, int? profile, DateTime? effectiveTo)
        {
            try
            {
                await _teachers.EndAssignmentAsync(assignmentId, DateTime.SpecifyKind(effectiveTo ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Assignment ended (history kept, BR-TCH-007). Marksheet/timetable re-pointing and parent notification arrive with the reassignment wizard.", "أُنهي الإسناد مع حفظ السجل (BR-TCH-007). إعادة توجيه الكشوف/الجدول وإشعار أولياء الأمور مع معالج إعادة الإسناد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Matrix), new { year, profile });
        }

        [HttpPost("matrix/copy")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Assignments, ActionVerb.Create)]
        public async Task<IActionResult> CopyFromPreviousYear(int? year, int? profile, int? sourceProfile)
        {
            try
            {
                if (profile == null || sourceProfile == null) throw new InvalidOperationException(T("Nothing to copy from.", "لا مصدر للنسخ."));
                var srcAssignments = await _db.TeacherAssignments.AsNoTracking().Where(a => a.EffectiveToUtc == null).Join(_db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == sourceProfile), a => a.CurriculumOfferingId, o => o.Id, (a, o) => new { a, o }).ToListAsync();
                var targetOfferings = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == profile && o.EffectiveToUtc == null).ToListAsync();
                var targetSections = await _db.Sections.AsNoTracking().Where(s => s.GradeYearProfileId == profile).OrderBy(s => s.NameEn).ToListAsync();
                var srcSections = await _db.Sections.AsNoTracking().Where(s => s.GradeYearProfileId == sourceProfile).OrderBy(s => s.NameEn).ToListAsync();
                var copied = 0; var skipped = 0;
                foreach (var x in srcAssignments)
                {
                    var to = targetOfferings.FirstOrDefault(o => o.SubjectId == x.o.SubjectId);
                    // sections are matched by name (3-A → 3-A); positional fallback by order
                    var srcSec = srcSections.FirstOrDefault(s => s.Id == x.a.SectionId);
                    var ts = srcSec == null ? null : targetSections.FirstOrDefault(s => s.NameEn == srcSec.NameEn) ?? (srcSections.IndexOf(srcSec) < targetSections.Count ? targetSections[srcSections.IndexOf(srcSec)] : null);
                    if (to == null || ts == null) { skipped++; continue; }
                    try { await _teachers.AssignAsync(x.a.TeacherProfileId, to.Id, ts.Id, x.a.Role, _clock.UtcNow.Date, overrideLoad: false); copied++; }
                    catch (InvalidOperationException) { skipped++; }
                }
                TempData["Flash"] = T($"Copied {copied} assignment(s); {skipped} skipped (no matching offering/section, duplicate primary, inactive contract or over load).", $"نُسخ {copied} إسناداً؛ تُخطي {skipped} (لا مادة/شعبة مطابقة، أساسي مكرر، عقد غير ساري أو تجاوز النصاب).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Matrix), new { year, profile });
        }

        // ================================================================== 8.3 Load board

        [HttpGet("load")]
        [RequirePermission(ScreenCatalog.Modules.Teachers, ScreenCatalog.Teachers.Load, ActionVerb.View)]
        public async Task<IActionResult> Load(int? year = null)
        {
            var (years, yr) = await YearsAsync(year);
            var profiles = await _db.TeacherProfiles.AsNoTracking().ToListAsync();
            var employees = await _db.Employees.AsNoTracking().Where(e => profiles.Select(p => p.EmployeeId).Contains(e.Id)).ToListAsync();
            var tas = yr == null ? new List<TeacherAssignment>() : await _db.TeacherAssignments.AsNoTracking().Where(a => a.AcademicYearId == yr.Id && a.EffectiveToUtc == null).ToListAsync();
            var offs = await _db.CurriculumOfferings.AsNoTracking().Where(o => tas.Select(a => a.CurriculumOfferingId).Contains(o.Id)).ToListAsync();
            var subs = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => offs.Select(o => o.SubjectId).Contains(s.Id)).ToListAsync();
            var secs = await _db.Sections.AsNoTracking().Where(s => tas.Select(a => a.SectionId).Contains(s.Id)).ToListAsync();
            var rows = profiles.Select(p =>
            {
                var e = employees.FirstOrDefault(x => x.Id == p.EmployeeId); if (e == null) return null;
                var list = tas.Where(a => a.TeacherProfileId == p.Id).Select(a => { var o = offs.FirstOrDefault(x => x.Id == a.CurriculumOfferingId); var s = o == null ? null : subs.FirstOrDefault(x => x.Id == o.SubjectId); var sec = secs.FirstOrDefault(x => x.Id == a.SectionId); return o == null || s == null || sec == null ? ((Sms.Domain.Subjects.Subject, Sms.Domain.Sections.Section, int, TeacherRole)?)null : (s, sec, o.WeeklyPeriods, a.Role); }).Where(x => x != null).Select(x => x!.Value).ToList();
                return new LoadBoardViewModel.Row(p, e, list.Sum(x => x.Item3), list);
            }).Where(r => r != null).Select(r => r!).OrderByDescending(r => r.Profile.MaxWeeklyPeriods == 0 ? 0 : (double)r.Load / r.Profile.MaxWeeklyPeriods).ToList();
            return View(new LoadBoardViewModel { Rows = rows, Year = yr, Years = years });
        }

        // ================================================================== helpers

        private async Task<(IReadOnlyList<AcademicYear> Years, AcademicYear? Year)> YearsAsync(int? yearId)
        {
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            var yr = years.FirstOrDefault(y => y.Id == (yearId ?? _workingYear.AcademicYearId)) ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active) ?? years.FirstOrDefault();
            return (years, yr);
        }

        private async Task<AssignmentMatrixViewModel> BuildMatrixAsync(int? yearId, int? profileId)
        {
            var (years, yr) = await YearsAsync(yearId);
            var m = new AssignmentMatrixViewModel { Years = years, Year = yr };
            if (yr == null) return m;
            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == yr.Id && p.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            m.Profiles = profiles.Select(p => new AssignmentMatrixViewModel.ProfileOption(p.Id, grades.First(g => g.Id == p.GradeLevelId))).OrderBy(p => p.Grade.SequenceOrder).ToList();
            m.Profile = m.Profiles.FirstOrDefault(p => p.ProfileId == profileId) ?? m.Profiles.FirstOrDefault();
            if (m.Profile == null) return m;

            var offerings = await _db.CurriculumOfferings.AsNoTracking().Where(o => o.GradeYearProfileId == m.Profile.ProfileId && o.EffectiveToUtc == null).ToListAsync();
            var subjects = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => offerings.Select(o => o.SubjectId).Contains(s.Id)).ToListAsync();
            m.Offerings = offerings.Select(o => (o, subjects.First(s => s.Id == o.SubjectId))).OrderBy(x => x.Item2.Code).ToList();
            m.Sections = await _db.Sections.AsNoTracking().Where(s => s.GradeYearProfileId == m.Profile.ProfileId).OrderBy(s => s.NameEn).ToListAsync();

            var tprofiles = await _db.TeacherProfiles.AsNoTracking().ToListAsync();
            var employees = await _db.Employees.AsNoTracking().Where(e => tprofiles.Select(p => p.EmployeeId).Contains(e.Id) && e.Status == EmployeeStatus.Active).ToListAsync();
            var allCurrent = await _db.TeacherAssignments.AsNoTracking().Where(a => a.AcademicYearId == yr.Id && a.EffectiveToUtc == null).ToListAsync();
            var allOffs = await _db.CurriculumOfferings.AsNoTracking().Where(o => allCurrent.Select(a => a.CurriculumOfferingId).Contains(o.Id)).ToListAsync();
            var userIds = employees.Where(e => e.UserAccountId != null).Select(e => e.UserAccountId!.Value).ToList();
            var quals = await _db.TeacherSubjectQualifications.AsNoTracking().Where(q => userIds.Contains(q.TeacherUserId)).ToListAsync();
            var teachers = tprofiles.Select(p =>
            {
                var e = employees.FirstOrDefault(x => x.Id == p.EmployeeId); if (e == null) return null;
                var load = TeacherLoadCalculator.CurrentLoad(allCurrent.Where(a => a.TeacherProfileId == p.Id).Select(a => allOffs.FirstOrDefault(o => o.Id == a.CurriculumOfferingId)?.WeeklyPeriods ?? 0).ToArray());
                var q = e.UserAccountId == null ? new List<int>() : quals.Where(x => x.TeacherUserId == e.UserAccountId).Select(x => x.SubjectId).Distinct().ToList();
                return new AssignmentMatrixViewModel.TeacherOption(p, e, load, e.UserAccountId == null, q);
            }).Where(t => t != null).Select(t => t!).OrderBy(t => IsArabic ? t.Employee.FirstNameAr : t.Employee.FirstNameEn).ToList();
            m.Teachers = teachers;

            var cells = new Dictionary<(int, int), IReadOnlyList<AssignmentMatrixViewModel.Cell>>();
            foreach (var a in allCurrent.Where(a => offerings.Any(o => o.Id == a.CurriculumOfferingId) && m.Sections.Any(s => s.Id == a.SectionId)))
            {
                var t = teachers.FirstOrDefault(x => x.Profile.Id == a.TeacherProfileId); if (t == null) continue;
                var key = (a.CurriculumOfferingId, a.SectionId);
                var list = cells.TryGetValue(key, out var existing) ? existing.ToList() : new List<AssignmentMatrixViewModel.Cell>();
                list.Add(new AssignmentMatrixViewModel.Cell(a, t));
                cells[key] = list.OrderBy(c => c.Assignment.Role).ToList();
            }
            m.Cells = cells;

            // copy-from-last-year: the same grade's profile in the most recent earlier year
            var grade = m.Profile.Grade;
            var prevYear = years.Where(y => y.StartDate < yr.StartDate).OrderByDescending(y => y.StartDate).FirstOrDefault();
            if (prevYear != null)
            {
                m.PreviousYearProfileId = (await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(p => p.AcademicYearId == prevYear.Id && p.GradeLevelId == grade.Id))?.Id;
            }
            return m;
        }
    }
}
