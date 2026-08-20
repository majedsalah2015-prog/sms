using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Students;
using Sms.Domain.Common;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/10 §8: student search/list, direct registration (the
    /// non-admissions path — transfers-in, opening data), and the Student
    /// File with the tabs this build can serve today: Personal (✎ T1 with
    /// reason, BR-STU-002), Parents & Guardians (link/unlink with the
    /// last-financially-responsible guard, BR-GLB-004), Emergency contacts,
    /// Academic history (enrollments + sections), Status (BR-WF-001
    /// transitions), Audit. Read-through tabs (medical, transport,
    /// attendance, fees, documents, certificates, behavior, activities…)
    /// show counts and open when their module screens land. Withdrawal
    /// wizard / ID-card batch / portal profile are deferred with them.
    /// </summary>
    [Route("students")]
    public class StudentsController : Controller
    {
        private readonly IStudentAdmin _students;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _workingYear;

        public StudentsController(IStudentAdmin students, AppDbContext db, IAuditContext audit, IClock clock, IWorkingYearContext workingYear)
        {
            _students = students;
            _db = db;
            _audit = audit;
            _clock = clock;
            _workingYear = workingYear;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        public async Task<IActionResult> Index(string? q = null, StudentStatus? status = null, int? grade = null)
        {
            var query = _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId);
            if (status != null) query = query.Where(s => s.Status == status);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(s => s.StudentNo.Contains(t) || s.FirstNameAr.Contains(t) || s.FamilyNameAr.Contains(t) || s.FirstNameEn.Contains(t) || s.FamilyNameEn.Contains(t) || (s.PrimaryIdNo != null && s.PrimaryIdNo.Contains(t)));
            }

            var total = await query.CountAsync();
            var students = await query.OrderBy(s => s.StudentNo).Take(200).ToListAsync();
            var ids = students.Select(s => s.Id).ToList();
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => ids.Contains(e.StudentId) && e.ExitDate == null).ToListAsync();
            var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => enrollments.Select(e => e.GradeYearProfileId).Contains(p.Id)).ToListAsync();
            var grades = await _db.GradeLevels.AsNoTracking().ToListAsync();
            var memberships = await _db.SectionMemberships.AsNoTracking().Where(m => enrollments.Select(e => e.Id).Contains(m.EnrollmentId) && m.EffectiveToUtc == null).ToListAsync();
            var sections = await _db.Sections.AsNoTracking().Where(s => memberships.Select(m => m.SectionId).Contains(s.Id)).ToListAsync();
            var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => ids.Contains(l.StudentId) && l.EffectiveToUtc == null && l.IsPrimaryContact).ToListAsync();
            var parents = await _db.Parents.AsNoTracking().Where(p => links.Select(l => l.ParentId).Contains(p.Id)).ToListAsync();
            var nats = await LookupAsync("Nationality");

            var rows = students.Select(s =>
            {
                var e = enrollments.Where(x => x.StudentId == s.Id).OrderByDescending(x => x.EnrollmentDate).FirstOrDefault();
                var p = e == null ? null : profiles.FirstOrDefault(x => x.Id == e.GradeYearProfileId);
                var g = p == null ? null : grades.FirstOrDefault(x => x.Id == p.GradeLevelId);
                var m = e == null ? null : memberships.FirstOrDefault(x => x.EnrollmentId == e.Id);
                var sec = m == null ? null : sections.FirstOrDefault(x => x.Id == m.SectionId);
                var pl = links.FirstOrDefault(l => l.StudentId == s.Id);
                var par = pl == null ? null : parents.FirstOrDefault(x => x.Id == pl.ParentId);
                var nat = nats.FirstOrDefault(n => n.Id == s.NationalityLookupId);
                return new StudentListViewModel.Row(s, g == null ? null : (IsArabic ? g.Name.NameAr : g.Name.NameEn), sec == null ? null : (IsArabic ? sec.NameAr : sec.NameEn), par == null ? null : (IsArabic ? par.NameAr : par.NameEn), nat == default ? "?" : (IsArabic ? nat.Ar : nat.En));
            }).Where(r => grade == null || (r.GradeName != null && grades.Any(g => g.Id == grade && (IsArabic ? g.Name.NameAr : g.Name.NameEn) == r.GradeName))).ToList();

            return View(new StudentListViewModel { Rows = rows, Query = q, Status = status, GradeId = grade, Grades = grades.OrderBy(g => g.SequenceOrder).ToList(), Total = total });
        }

        [HttpGet("new")]
        public async Task<IActionResult> Register()
        {
            return View(await BuildFormAsync(null));
        }

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(StudentFormViewModel form)
        {
            try
            {
                RequireNames(form);
                if (form.DateOfBirth == null || form.NationalityLookupId == null) throw new InvalidOperationException(T("Date of birth and nationality are required.", "تاريخ الميلاد والجنسية مطلوبان."));
                var student = await _students.RegisterStudentAsync(form.FirstNameAr!, form.FatherNameAr!, form.GrandfatherNameAr!, form.FamilyNameAr!, form.FirstNameEn!, form.FatherNameEn!, form.GrandfatherNameEn!, form.FamilyNameEn!,
                    form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, form.PrimaryIdTypeLookupId, string.IsNullOrWhiteSpace(form.PrimaryIdNo) ? null : form.PrimaryIdNo.Trim(), form.PrimaryIdExpiry);
                if (form.ParentId != null && form.RelationshipLookupId != null)
                {
                    await _students.LinkGuardianAsync(student.Id, form.ParentId.Value, form.RelationshipLookupId.Value, true, true, true, true, _clock.UtcNow);
                }

                if (form.GradeYearProfileId != null)
                {
                    await _students.EnrollAsync(student.Id, form.GradeYearProfileId.Value, _clock.UtcNow.Date, EnrollmentSourceType.Admission);
                }

                TempData["Flash"] = T($"Student {student.StudentNo} registered.", $"تم تسجيل الطالب {student.StudentNo}.");
                return RedirectToAction(nameof(File), new { id = student.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var model = await BuildFormAsync(null);
                Copy(form, model);
                return View(model);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> File(int id, string? tab = null)
        {
            var model = await BuildFileAsync(id);
            if (model == null) return NotFound();
            model.ActiveTab = tab ?? "personal";
            return View(model);
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentFormViewModel form)
        {
            try
            {
                RequireNames(form);
                if (form.DateOfBirth == null || form.NationalityLookupId == null) throw new InvalidOperationException(T("Date of birth and nationality are required.", "تاريخ الميلاد والجنسية مطلوبان."));
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
                await _students.UpdateStudentAsync(id, form.FirstNameAr!, form.FatherNameAr!, form.GrandfatherNameAr!, form.FamilyNameAr!, form.FirstNameEn!, form.FatherNameEn!, form.GrandfatherNameEn!, form.FamilyNameEn!,
                    form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, form.PrimaryIdTypeLookupId, string.IsNullOrWhiteSpace(form.PrimaryIdNo) ? null : form.PrimaryIdNo.Trim(), form.PrimaryIdExpiry);
                TempData["Flash"] = T("Student file updated.", "تم تحديث ملف الطالب.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        [HttpPost("{id:int}/status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Status(int id, StudentStatus target, string? reason)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                await _students.ChangeStatusAsync(id, target);
                TempData["Flash"] = T($"Status changed to {target}.", $"تغيرت الحالة إلى {target}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(File), new { id, tab = "status" });
        }

        [HttpPost("{id:int}/guardian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkGuardian(int id, int? parentId, int? relationshipId, bool isPrimary, bool isFinancial, bool isPickup, bool isPortal, DateTime? effectiveFrom)
        {
            try
            {
                if (parentId == null || relationshipId == null) throw new InvalidOperationException(T("Parent and relationship are required.", "ولي الأمر وصلة القرابة مطلوبان."));
                await _students.LinkGuardianAsync(id, parentId.Value, relationshipId.Value, isPrimary, isFinancial, isPickup, isPortal, DateTime.SpecifyKind(effectiveFrom ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Guardian linked.", "تم ربط ولي الأمر.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(File), new { id, tab = "guardians" });
        }

        [HttpPost("{id:int}/guardian/{linkId:int}/unlink")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlinkGuardian(int id, int linkId, DateTime? effectiveTo)
        {
            try
            {
                await _students.UnlinkGuardianAsync(linkId, DateTime.SpecifyKind(effectiveTo ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Guardian link ended (history kept).", "تم إنهاء ربط ولي الأمر (مع حفظ السجل).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(File), new { id, tab = "guardians" });
        }

        [HttpPost("{id:int}/emergency")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEmergencyContact(int id, string? nameAr, string? nameEn, string? phone, bool isPickup, int? relationshipId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn) || string.IsNullOrWhiteSpace(phone)) throw new InvalidOperationException(T("Both names and a phone are required.", "الاسمان والهاتف مطلوبة."));
                await _students.AddEmergencyContactAsync(id, nameAr.Trim(), nameEn.Trim(), phone.Trim(), isPickup, relationshipId);
                TempData["Flash"] = T("Emergency contact added.", "تمت إضافة جهة اتصال الطوارئ.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(File), new { id, tab = "emergency" });
        }

        [HttpPost("{id:int}/enroll")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enroll(int id, int? gradeYearProfileId, DateTime? enrollmentDate, EnrollmentSourceType sourceType)
        {
            try
            {
                if (gradeYearProfileId == null) throw new InvalidOperationException(T("Choose a grade-year.", "اختر الصف السنوي."));
                await _students.EnrollAsync(id, gradeYearProfileId.Value, enrollmentDate ?? _clock.UtcNow.Date, sourceType);
                TempData["Flash"] = T("Enrollment recorded.", "تم تسجيل القيد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(File), new { id, tab = "academic" });
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? q, StudentStatus? status, int? grade)
        {
            var s = await _db.Students.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SchoolId == _db.CurrentSchoolId);
            if (s == null) return NotFound();
            try
            {
                await _students.DeleteStudentAsync(id);
                TempData["Flash"] = T($"Student {s.StudentNo} deleted.", $"تم حذف الطالب {s.StudentNo}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { q, status, grade });
        }

        // ------------------------------------------------------------------

        private async Task<StudentFileViewModel?> BuildFileAsync(int id)
        {
            var s = await _db.Students.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SchoolId == _db.CurrentSchoolId);
            if (s == null) return null;
            var nats = await LookupAsync("Nationality"); var idTypes = await LookupAsync("IdType"); var rels = await LookupAsync("RelationshipType");
            var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => l.StudentId == id).OrderByDescending(l => l.EffectiveFromUtc).ToListAsync();
            var parents = await _db.Parents.IgnoreQueryFilters().AsNoTracking().Where(p => links.Select(l => l.ParentId).Contains(p.Id)).ToListAsync();
            StudentFileViewModel.GuardianRow G(StudentGuardianLink l) => new(l, parents.First(p => p.Id == l.ParentId), rels.FirstOrDefault(r => r.Id == l.RelationshipLookupId) is var r && r != default ? (IsArabic ? r.Ar : r.En) : "?");
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => e.StudentId == id).OrderByDescending(e => e.EnrollmentDate).ToListAsync();
            var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => enrollments.Select(e => e.GradeYearProfileId).Contains(p.Id)).ToListAsync();
            var years = await _db.AcademicYears.AsNoTracking().ToListAsync();
            var grades = await _db.GradeLevels.AsNoTracking().ToListAsync();
            var memberships = await _db.SectionMemberships.AsNoTracking().Where(m => enrollments.Select(e => e.Id).Contains(m.EnrollmentId) && m.EffectiveToUtc == null).ToListAsync();
            var sections = await _db.Sections.AsNoTracking().Where(x => memberships.Select(m => m.SectionId).Contains(x.Id)).ToListAsync();
            var audit = await _db.AuditEntries.AsNoTracking().Where(e => e.EntityType == nameof(Student) && e.EntityId == id).OrderByDescending(e => e.OccurredAtUtc).Take(100).ToListAsync();
            var allProfiles = await _db.GradeYearProfiles.AsNoTracking().ToListAsync();

            return new StudentFileViewModel
            {
                Student = s,
                NationalityName = nats.FirstOrDefault(n => n.Id == s.NationalityLookupId) is var n && n != default ? (IsArabic ? n.Ar : n.En) : "?",
                IdTypeName = s.PrimaryIdTypeLookupId == null ? null : (idTypes.FirstOrDefault(t => t.Id == s.PrimaryIdTypeLookupId) is var t && t != default ? (IsArabic ? t.Ar : t.En) : "?"),
                Guardians = links.Where(l => l.EffectiveToUtc == null).Select(G).ToList(),
                PastGuardians = links.Where(l => l.EffectiveToUtc != null).Select(G).ToList(),
                EmergencyContacts = await _db.EmergencyContacts.AsNoTracking().Where(c => c.StudentId == id).ToListAsync(),
                Enrollments = enrollments.Select(e =>
                {
                    var p = profiles.First(x => x.Id == e.GradeYearProfileId);
                    var m = memberships.FirstOrDefault(x => x.EnrollmentId == e.Id);
                    return new StudentFileViewModel.EnrollmentRow(e, years.First(y => y.Id == e.AcademicYearId), grades.First(g => g.Id == p.GradeLevelId), m == null ? null : sections.FirstOrDefault(x => x.Id == m.SectionId));
                }).ToList(),
                AllowedTransitions = Enum.GetValues<StudentStatus>().Where(t => StudentStatusTransitions.CanTransition(s.Status, t)).ToList(),
                Audit = audit.Select(a => (a.Action.ToString(), a.FieldName, a.OldValue, a.NewValue, a.OccurredAtUtc, a.ActorUserId, a.Reason)).ToList(),
                Parents = await _db.Parents.AsNoTracking().OrderBy(p => p.NameEn).Take(500).ToListAsync(),
                Relationships = rels, Nationalities = nats, IdTypes = idTypes,
                Profiles = allProfiles.Select(p => { var g = grades.First(x => x.Id == p.GradeLevelId); var y = years.First(x => x.Id == p.AcademicYearId); return (p.Id, g.Name.NameAr, g.Name.NameEn, y.LabelAr, y.LabelEn); }).OrderByDescending(x => x.Item5).ToList(),
                ReadThroughCounts = new Dictionary<string, int>
                {
                    ["attendance"] = await _db.AttendanceDays.AsNoTracking().CountAsync(a => enrollments.Select(e => e.Id).Contains(a.EnrollmentId)),
                    ["charges"] = await _db.Charges.AsNoTracking().CountAsync(c => c.StudentId == id),
                    ["certificates"] = await _db.CertificateIssues.AsNoTracking().CountAsync(c => c.StudentId == id),
                },
            };
        }

        private async Task<StudentFormViewModel> BuildFormAsync(Student? s)
        {
            var grades = await _db.GradeLevels.AsNoTracking().ToListAsync();
            var years = await _db.AcademicYears.AsNoTracking().ToListAsync();
            var profiles = await _db.GradeYearProfiles.AsNoTracking().ToListAsync();
            var m = new StudentFormViewModel
            {
                Nationalities = await LookupAsync("Nationality"), IdTypes = await LookupAsync("IdType"), Relationships = await LookupAsync("RelationshipType"),
                Parents = await _db.Parents.AsNoTracking().OrderBy(p => p.NameEn).Take(500).ToListAsync(),
                Profiles = profiles.Select(p => { var g = grades.First(x => x.Id == p.GradeLevelId); var y = years.First(x => x.Id == p.AcademicYearId); return (p.Id, g.Name.NameAr, g.Name.NameEn, y.LabelAr, y.LabelEn); }).OrderByDescending(x => x.Item5).ToList(),
            };
            var working = profiles.Where(p => p.AcademicYearId == _workingYear.AcademicYearId).ToList();
            if (working.Count == 1) m.GradeYearProfileId = working[0].Id;
            return m;
        }

        private static void Copy(StudentFormViewModel from, StudentFormViewModel to)
        {
            to.FirstNameAr = from.FirstNameAr; to.FatherNameAr = from.FatherNameAr; to.GrandfatherNameAr = from.GrandfatherNameAr; to.FamilyNameAr = from.FamilyNameAr;
            to.FirstNameEn = from.FirstNameEn; to.FatherNameEn = from.FatherNameEn; to.GrandfatherNameEn = from.GrandfatherNameEn; to.FamilyNameEn = from.FamilyNameEn;
            to.Gender = from.Gender; to.DateOfBirth = from.DateOfBirth; to.NationalityLookupId = from.NationalityLookupId; to.PrimaryIdTypeLookupId = from.PrimaryIdTypeLookupId; to.PrimaryIdNo = from.PrimaryIdNo; to.PrimaryIdExpiry = from.PrimaryIdExpiry;
            to.GradeYearProfileId = from.GradeYearProfileId; to.ParentId = from.ParentId; to.RelationshipLookupId = from.RelationshipLookupId;
        }

        private static void RequireNames(StudentFormViewModel f)
        {
            foreach (var (v, n) in new[] { (f.FirstNameAr, "First name (Arabic)"), (f.FatherNameAr, "Father name (Arabic)"), (f.GrandfatherNameAr, "Grandfather name (Arabic)"), (f.FamilyNameAr, "Family name (Arabic)"), (f.FirstNameEn, "First name (English)"), (f.FatherNameEn, "Father name (English)"), (f.GrandfatherNameEn, "Grandfather name (English)"), (f.FamilyNameEn, "Family name (English)") })
            {
                if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{n} is required (BR-GLB-001).", $"الحقل {n} مطلوب (BR-GLB-001)."));
            }
        }

        private async Task<IReadOnlyList<(int Id, string Ar, string En)>> LookupAsync(string category)
        {
            var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == category);
            return cat == null ? Array.Empty<(int, string, string)>() : await _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == cat.Id).OrderBy(v => v.SortOrder).Select(v => new ValueTuple<int, string, string>(v.Id, v.Name.NameAr, v.Name.NameEn)).ToListAsync();
        }
    }
}
