using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Parents;
using Sms.Application.Students;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Sections;
using Sms.Domain.Geography;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;
using Sms.Web.Services;

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
    public partial class StudentsController : Controller
    {
        private readonly IStudentAdmin _students;

        /// <summary>The import creates guardian files as it goes; nothing else on this controller writes one.</summary>
        private readonly IParentAdmin _parents;

        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _workingYear;
        private readonly IPermissionService _permissions;
        private readonly Sms.Web.Services.PersonPhotoService _photos;

        /// <summary>The one gate every file in the product passes through — the photograph above and the documents tab below use it alike.</summary>
        private readonly Sms.Web.Services.AttachmentIntake _intake;

        private readonly ICurrentUser _currentUser;

        /// <summary>
        /// Seating a child in a section is a Sections operation with its own rules (capacity,
        /// gender policy, the transfer's reason code). The placement screen reaches it through the
        /// same port the section's own page uses rather than writing memberships itself — two
        /// screens writing the same table by two different sets of checks is how one of them ends
        /// up wrong.
        /// </summary>
        private readonly Sms.Application.Sections.ISectionAdmin _sections;

        /// <summary>What an attachment calls this record. Written once so a typo cannot detach a whole file's documents.</summary>
        private const string StudentEntity = "Student";

        public StudentsController(IStudentAdmin students, IParentAdmin parents, AppDbContext db, IAuditContext audit, IClock clock, IWorkingYearContext workingYear, IPermissionService permissions, Sms.Web.Services.PersonPhotoService photos, Sms.Web.Services.AttachmentIntake intake, ICurrentUser currentUser, Sms.Application.Sections.ISectionAdmin sections)
        {
            _sections = sections;
            _intake = intake;
            _currentUser = currentUser;
            _students = students;
            _parents = parents;
            _db = db;
            _audit = audit;
            _clock = clock;
            _workingYear = workingYear;
            _permissions = permissions;
            _photos = photos;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        /// <summary>
        /// Rows the on-screen directory draws before it stops. The list is a finding instrument,
        /// not a register: past a couple of hundred rows nobody is reading, they are filtering.
        /// The printed sheet and the export are the register, and they are not capped.
        /// </summary>
        private const int DirectoryPageSize = 200;

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.View)]
        public async Task<IActionResult> Index(
            string? q = null, StudentStatus? status = null, int? grade = null, int? section = null, Gender? gender = null)
        {
            var directory = await BuildDirectoryAsync(q, status, grade, section, gender, DirectoryPageSize);

            return View(new StudentListViewModel
            {
                Rows = directory.Rows, Query = q, Status = status, GradeId = grade,
                SectionId = section, Gender = gender,
                Grades = directory.Grades, Sections = directory.Sections, Total = directory.Total,
                CanPlace = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View, HttpContext.RequestAborted),
                CanPrint = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Print, HttpContext.RequestAborted),
                CanExport = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Export, HttpContext.RequestAborted),
            });
        }

        // ================================================================== the register, out of the screen

        /// <summary>
        /// The students register as a sheet somebody signs or files (doc/Modules/10 §10 —
        /// "Students register by grade/section/status").
        /// <para>
        /// It takes the directory's own filters verbatim rather than offering a second set: what a
        /// registrar prints is what they were just looking at, and a print screen that asks the
        /// question again is a place for the two answers to disagree. Uncapped, unlike the screen —
        /// a register that silently stops at row 200 is worse than a slow one, because nothing on
        /// the paper says it stopped.
        /// </para>
        /// <para>
        /// <b>Not a PDF.</b> There is no PDF engine in this build (a pending owner decision), so
        /// this is the browser's own print of an HTML sheet, as every other printable document here
        /// is. The layout hides the application chrome and keeps the school's name, the filter and
        /// the moment it was taken on the page.
        /// </para>
        /// </summary>
        [HttpGet("print")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Print)]
        public async Task<IActionResult> Print(
            string? q = null, StudentStatus? status = null, int? grade = null, int? section = null, Gender? gender = null)
        {
            var directory = await BuildDirectoryAsync(q, status, grade, section, gender, take: null);
            var school = await _db.Schools.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => new { s.NameAr, s.NameEn })
                .SingleOrDefaultAsync(HttpContext.RequestAborted);

            return View(new StudentRosterViewModel
            {
                Rows = directory.Rows,
                Total = directory.Total,
                SchoolName = school == null ? string.Empty : (IsArabic ? school.NameAr : school.NameEn),
                PrintedAtUtc = _clock.UtcNow,
                Filters = DescribeFilters(directory, q, status, grade, section, gender),
            });
        }

        /// <summary>
        /// The same register as a file (doc/Modules/10 §8 — the list is "export-gated", and §6 puts
        /// the export behind its own right rather than behind View).
        /// <para>
        /// CSV rather than a spreadsheet format because the destination is always another system —
        /// a ministry return, a bus company's list, a mail merge — and every one of them reads CSV.
        /// The byte-order mark and the quoting are <see cref="StudentDirectoryExport"/>'s business
        /// and are pinned by tests there.
        /// </para>
        /// </summary>
        [HttpGet("export.csv")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Export)]
        public async Task<IActionResult> ExportCsv(
            string? q = null, StudentStatus? status = null, int? grade = null, int? section = null, Gender? gender = null)
        {
            var directory = await BuildDirectoryAsync(q, status, grade, section, gender, take: null);
            var arabic = IsArabic;

            var records = new List<IEnumerable<string?>> { StudentDirectoryExport.Headings(arabic) };
            records.AddRange(directory.Rows.Select(r => new[]
            {
                r.Student.StudentNo,
                arabic
                    ? $"{r.Student.FirstNameAr} {r.Student.FatherNameAr} {r.Student.FamilyNameAr}"
                    : $"{r.Student.FirstNameEn} {r.Student.FatherNameEn} {r.Student.FamilyNameEn}",
                Labels.Gender(r.Student.Gender, arabic),
                r.Student.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.GradeName ?? string.Empty,
                r.SectionName ?? string.Empty,
                r.PrimaryParent ?? string.Empty,
                r.Student.Mobile ?? string.Empty,
                Labels.StudentStatus(r.Student.Status, arabic),
            }.AsEnumerable()));

            return File(
                StudentDirectoryExport.Bytes(records),
                "text/csv",
                StudentDirectoryExport.FileName(_clock.UtcNow));
        }

        // ------------------------------------------------------------------ one query, three readings

        /// <summary>
        /// Which students match, and what to call their grade and section — asked once for the
        /// screen, the printed register and the export alike.
        /// <para>
        /// Three surfaces answering "which students" by three copies of the same joins is how a
        /// printed roll comes to hold a child the screen did not show. The filters therefore live
        /// here and nowhere else.
        /// </para>
        /// <para>
        /// Grade and section are filtered <b>in the database</b>, through the child's live
        /// enrollment. They used to be applied in memory to the page of 200 the screen had already
        /// taken, which meant picking a grade searched the first 200 students by number rather than
        /// the school, and the count above the table ignored the choice entirely.
        /// </para>
        /// <para>
        /// The two id sets are read out first rather than left as correlated subqueries, because
        /// the students query runs under <c>IgnoreQueryFilters</c> — it carries its own explicit
        /// <c>SchoolId</c> test — and a subquery inside it would lose the tenant filter with it.
        /// Read separately, <c>Enrollments</c> and <c>SectionMemberships</c> keep theirs. Both sets
        /// are bounded by one grade or one section of one school.
        /// </para>
        /// </summary>
        private async Task<DirectoryPage> BuildDirectoryAsync(
            string? q, StudentStatus? status, int? grade, int? section, Gender? gender, int? take)
        {
            var ct = HttpContext.RequestAborted;
            var query = _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId);
            if (status != null) query = query.Where(s => s.Status == status);
            if (gender != null) query = query.Where(s => s.Gender == gender);
            if (!string.IsNullOrWhiteSpace(q))
            {
                // The mobile is searched as well as shown. A number is what a caller gives when
                // they cannot spell the child's name, and a column you can read but not search is
                // half a column.
                var t = q.Trim();
                query = query.Where(s => s.StudentNo.Contains(t) || s.FirstNameAr.Contains(t) || s.FamilyNameAr.Contains(t) || s.FirstNameEn.Contains(t) || s.FamilyNameEn.Contains(t) || (s.PrimaryIdNo != null && s.PrimaryIdNo.Contains(t)) || (s.Mobile != null && s.Mobile.Contains(t)));
            }

            if (grade != null || section != null)
            {
                var live = _db.Enrollments.AsNoTracking().Where(e => e.ExitDate == null);

                if (grade != null)
                {
                    var profileIds = await _db.GradeYearProfiles.AsNoTracking()
                        .Where(p => p.GradeLevelId == grade).Select(p => p.Id).ToListAsync(ct);
                    live = live.Where(e => profileIds.Contains(e.GradeYearProfileId));
                }

                if (section != null)
                {
                    var seated = await _db.SectionMemberships.AsNoTracking()
                        .Where(m => m.SectionId == section && m.EffectiveToUtc == null)
                        .Select(m => m.EnrollmentId).ToListAsync(ct);
                    live = live.Where(e => seated.Contains(e.Id));
                }

                var matched = await live.Select(e => e.StudentId).Distinct().ToListAsync(ct);
                query = query.Where(s => matched.Contains(s.Id));
            }

            var total = await query.CountAsync(ct);
            var ordered = query.OrderBy(s => s.StudentNo);
            var students = take == null
                ? await ordered.ToListAsync(ct)
                : await ordered.Take(take.Value).ToListAsync(ct);

            var ids = students.Select(s => s.Id).ToList();
            var enrollments = await _db.Enrollments.AsNoTracking().Where(e => ids.Contains(e.StudentId) && e.ExitDate == null).ToListAsync(ct);
            var profiles = await _db.GradeYearProfiles.AsNoTracking().Where(p => enrollments.Select(e => e.GradeYearProfileId).Contains(p.Id)).ToListAsync(ct);

            // Past the soft-active filter: a retired grade still names the year a child is already
            // sitting in, and the row must say so. The picker below is built from the active ones.
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync(ct);
            var memberships = await _db.SectionMemberships.AsNoTracking().Where(m => enrollments.Select(e => e.Id).Contains(m.EnrollmentId) && m.EffectiveToUtc == null).ToListAsync(ct);
            var sections = await _db.Sections.AsNoTracking().Where(s => memberships.Select(m => m.SectionId).Contains(s.Id)).ToListAsync(ct);
            var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => ids.Contains(l.StudentId) && l.EffectiveToUtc == null && l.IsPrimaryContact).ToListAsync(ct);
            var parents = await _db.Parents.AsNoTracking().Where(p => links.Select(l => l.ParentId).Contains(p.Id)).ToListAsync(ct);

            var rows = students.Select(s =>
            {
                var e = enrollments.Where(x => x.StudentId == s.Id).OrderByDescending(x => x.EnrollmentDate).FirstOrDefault();
                var p = e == null ? null : profiles.FirstOrDefault(x => x.Id == e.GradeYearProfileId);
                var g = p == null ? null : grades.FirstOrDefault(x => x.Id == p.GradeLevelId);
                var m = e == null ? null : memberships.FirstOrDefault(x => x.EnrollmentId == e.Id);
                var sec = m == null ? null : sections.FirstOrDefault(x => x.Id == m.SectionId);
                var pl = links.FirstOrDefault(l => l.StudentId == s.Id);
                var par = pl == null ? null : parents.FirstOrDefault(x => x.Id == pl.ParentId);
                return new StudentListViewModel.Row(s, g == null ? null : (IsArabic ? g.Name.NameAr : g.Name.NameEn), sec == null ? null : (IsArabic ? sec.NameAr : sec.NameEn), par == null ? null : (IsArabic ? par.NameAr : par.NameEn));
            }).ToList();

            return new DirectoryPage(rows, total, grades.OrderBy(g => g.SequenceOrder).ToList(), await SectionOptionsAsync(grades, ct));
        }

        /// <summary>
        /// The working year's sections, for the filter. Closed ones are left out of the picker
        /// (BR-SCN-007 keeps them in history, which is a different question from what a registrar
        /// filters by today), and a grade's own name comes from the unfiltered list so a section of
        /// a retired grade still reads as that grade rather than as a bare letter.
        /// </summary>
        private async Task<IReadOnlyList<StudentListViewModel.SectionOption>> SectionOptionsAsync(
            IReadOnlyList<GradeLevel> grades, System.Threading.CancellationToken ct)
        {
            var year = _workingYear.AcademicYearId;
            var sections = await _db.Sections.AsNoTracking()
                .Where(s => s.AcademicYearId == year && s.Status == SectionStatus.Active)
                .ToListAsync(ct);
            if (sections.Count == 0) return Array.Empty<StudentListViewModel.SectionOption>();

            // Read through the filters, not past them. GradeYearProfile carries an IsActive column
            // but is not ISoftActiveFiltered, so there is no soft-active filter here to escape —
            // IgnoreQueryFilters would drop only the tenant filter, which is the one that has to
            // hold. The profile a section names is this school's by construction anyway.
            var profileIds = sections.Select(s => s.GradeYearProfileId).Distinct().ToList();
            var profiles = await _db.GradeYearProfiles.AsNoTracking()
                .Where(p => profileIds.Contains(p.Id)).ToListAsync(ct);

            return sections.Select(s =>
            {
                var g = profiles.FirstOrDefault(p => p.Id == s.GradeYearProfileId) is { } p
                    ? grades.FirstOrDefault(x => x.Id == p.GradeLevelId)
                    : null;
                return new StudentListViewModel.SectionOption(
                    s.Id,
                    IsArabic ? s.NameAr : s.NameEn,
                    g == null ? T("Ungraded", "بلا صف") : (IsArabic ? g.Name.NameAr : g.Name.NameEn),
                    g?.SequenceOrder ?? int.MaxValue);
            })
            .OrderBy(o => o.GradeOrder).ThenBy(o => o.Name, StringComparer.CurrentCulture)
            .ToList();
        }

        /// <summary>What the sheet and the file were taken under, in the reader's language.</summary>
        private IReadOnlyList<string> DescribeFilters(
            DirectoryPage directory, string? q, StudentStatus? status, int? grade, int? section, Gender? gender)
            => StudentDirectoryExport.Describe(
                IsArabic,
                q,
                status == null ? null : Labels.StudentStatus(status.Value, IsArabic),
                grade == null
                    ? null
                    : directory.Grades.FirstOrDefault(g => g.Id == grade) is { } g ? (IsArabic ? g.Name.NameAr : g.Name.NameEn) : null,
                section == null
                    ? null
                    : directory.Sections.FirstOrDefault(s => s.Id == section) is { } s ? $"{s.GradeName} / {s.Name}" : null,
                gender == null ? null : Labels.Gender(gender.Value, IsArabic));

        /// <summary>The directory's answer: the rows, how many matched in all, and the two pickers.</summary>
        private sealed record DirectoryPage(
            IReadOnlyList<StudentListViewModel.Row> Rows,
            int Total,
            IReadOnlyList<GradeLevel> Grades,
            IReadOnlyList<StudentListViewModel.SectionOption> Sections);

        [HttpGet("new")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Register()
        {
            return View(await BuildFormAsync(null));
        }

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Register(StudentFormViewModel form, IFormFile? photo = null)
        {
            try
            {
                RequireNames(form);
                if (form.DateOfBirth == null || form.NationalityLookupId == null) throw new InvalidOperationException(T("Date of birth and nationality are required.", "تاريخ الميلاد والجنسية مطلوبان."));

                // The photograph is judged before the student exists. A file that is going to be
                // refused must not leave a registered student behind it — the registrar would have
                // to find the half-made record and finish it from the file screen, and the second
                // attempt at this form is how a child ends up in the register twice.
                var rejection = photo == null ? FileRejection.None : PersonPhotoService.Inspect(photo);
                if (rejection != FileRejection.None) throw new InvalidOperationException(Labels.FileRejection(rejection, IsArabic, PersonPhotoService.PhotoFormats, PersonPhotoService.MaxPhotoBytes));

                var student = await _students.RegisterStudentAsync(form.FirstNameAr!, form.FatherNameAr!, form.GrandfatherNameAr!, form.FamilyNameAr!, form.FirstNameEn!, form.FatherNameEn!, form.GrandfatherNameEn!, form.FamilyNameEn!,
                    form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, form.PrimaryIdTypeLookupId, string.IsNullOrWhiteSpace(form.PrimaryIdNo) ? null : form.PrimaryIdNo.Trim(), form.PrimaryIdExpiry);

                if (photo != null)
                {
                    // Inspect() has already passed, so what is left is the document type's own
                    // upload policy — a school may have tightened it. The student is registered
                    // either way: undoing a numbered record because a JPEG was refused would cost
                    // a student number that is never re-issued (BR-NUM-004).
                    try
                    {
                        await AttachPhotoAsync(student.Id, photo);
                    }
                    catch (Sms.Application.Common.Exceptions.AttachmentPolicyViolationException)
                    {
                        TempData["Error"] = T("The student was registered, but the photo was not accepted — add it from the file.", "تم تسجيل الطالب، لكن الصورة لم تُقبل — أضفها من ملف الطالب.");
                    }
                }

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
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                var model = await BuildFormAsync(null);
                Copy(form, model);
                return View(model);
            }
        }

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View)]
        public async Task<IActionResult> File(int id, string? tab = null)
        {
            var model = await BuildFileAsync(id);
            if (model == null) return NotFound();
            model.ActiveTab = tab ?? "personal";

            // BR-GLB-072: the social profile has its own permission and no screen of its own, so the
            // file has to ask on its behalf. Without this, holding STU/File/View would hand over a
            // family's circumstances, and the separate permission would exist only on paper.
            model.CanSeeSocialProfile = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Students, ScreenCatalog.Students.SocialProfile, ActionVerb.View, HttpContext.RequestAborted);
            model.CanEditSocialProfile = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Students, ScreenCatalog.Students.SocialProfile, ActionVerb.Edit, HttpContext.RequestAborted);

            if (!model.CanSeeSocialProfile && model.ActiveTab == "social")
            {
                model.ActiveTab = "personal";
            }

            // BR-ATT-004: the documents inherit this screen's access, and the restricted types
            // follow the same permission as the rest of the sensitive data on the file.
            model.Documents = new EntityDocumentsViewModel
            {
                Controller = "Students",
                OwnerId = id,
                OwnerName = IsArabic ? model.Student.FirstNameAr : model.Student.FirstNameEn,
                Rows = await _intake.ListAsync(StudentEntity, id, model.CanSeeSocialProfile, HttpContext.RequestAborted),
                Types = await _intake.TypesForAsync(ScreenCatalog.Modules.Students, model.CanSeeSocialProfile, HttpContext.RequestAborted),
                CanEdit = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Edit, HttpContext.RequestAborted),
                CanVerify = await _permissions.HasPermissionAsync(
                    ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Approve, HttpContext.RequestAborted),
            };

            return View(model);
        }

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Edit)]
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
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        [HttpPost("{id:int}/social")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.SocialProfile, ActionVerb.Edit)]
        public async Task<IActionResult> UpdateSocialProfile(int id, StudentFormViewModel form)
        {
            try
            {
                // T1 with a mandatory reason, like the identity fields: each of these feeds a decision
                // the school has to defend, and someone will be asked why it changed.
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
                await _students.UpdateSocialProfileAsync(
                    id,
                    form.Religion,
                    form.ResidencyStatus, form.FinancialStatus, form.RationCardNo,
                    form.PlaceOfBirth, form.FamilySize, form.BirthOrder, form.SiblingCount, form.Mobile,
                    HttpContext.RequestAborted);
                TempData["Flash"] = T("Social profile updated.", "تم تحديث البيانات الاجتماعية.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(File), new { id, tab = "social" });
        }

        // ------------------------------------------------------------------ photograph
        //
        // Served from an action rather than written into the page as a data URI: a photo is the one
        // piece of a student's file that a browser can cache on its own, and inlining it would put a
        // face into every HTML response whether or not anyone was looking at it.

        [HttpGet("{id:int}/photo")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View)]
        public async Task<IActionResult> Photo(int id)
        {
            var photoId = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == id && s.SchoolId == _db.CurrentSchoolId)
                .Select(s => s.PhotoAttachmentId)
                .SingleOrDefaultAsync();

            var photo = await _photos.ReadAsync(photoId, HttpContext.RequestAborted);
            if (photo == null) { return NotFound(); }

            return File(photo.Content, photo.ContentType);
        }

        [HttpPost("{id:int}/photo")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Edit)]
        public async Task<IActionResult> UploadPhoto(int id, IFormFile? photo)
        {
            try
            {
                if (!await _db.Students.AnyAsync(s => s.Id == id, HttpContext.RequestAborted)) { return NotFound(); }

                await AttachPhotoAsync(id, photo!);
                TempData["Flash"] = T("Photo updated.", "تم تحديث الصورة.");
            }
            // The policy exception is an InvalidOperationException, so it has to be caught first or
            // its own message — which names a rule rather than a fact — is what reaches the screen.
            catch (Sms.Application.Common.Exceptions.AttachmentPolicyViolationException)
            {
                TempData["Error"] = T("That file is not an acceptable photo.", "هذا الملف ليس صورة مقبولة.");
            }
            // Also an InvalidOperationException, and it carries a reason rather than a sentence —
            // the wording is chosen here, in the reader's language, never thrown from the service.
            catch (FileRejectedException ex) { TempData["Error"] = Labels.FileRejection(ex.Rejection, IsArabic, ex.AllowedFormats, ex.MaxBytes); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        [HttpPost("{id:int}/photo/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Edit)]
        public async Task<IActionResult> RemovePhoto(int id)
        {
            var student = await _db.Students.SingleOrDefaultAsync(s => s.Id == id);
            if (student == null) { return NotFound(); }

            // The pointer is cleared; the attachment itself stays, because doc 10 does not delete
            // files while the record that owned them exists (BR-ATT-007).
            student.PhotoAttachmentId = null;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            TempData["Flash"] = T("Photo removed.", "تمت إزالة الصورة.");
            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        /// <summary>
        /// Stores the file as the student's photograph and points the record at it. Shared by the
        /// registration screen and the file screen so both mean the same thing by "photo".
        /// </summary>
        private async Task AttachPhotoAsync(int studentId, IFormFile file)
        {
            var attachmentId = await _photos.SaveAsync(
                file, StudentEntity, studentId, ScreenCatalog.Modules.Students, HttpContext.RequestAborted);

            var student = await _db.Students.SingleAsync(s => s.Id == studentId, HttpContext.RequestAborted);
            student.PhotoAttachmentId = attachmentId;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
        }

        // ------------------------------------------------------------------ documents
        //
        // doc 10 §5 "Entity documents tab". The same partial, the same intake and the same four
        // verbs the employee file uses — a birth certificate and a contract are filed by one
        // mechanism, so the school learns it once. Access inherits from this screen (BR-ATT-004):
        // whoever may open the student's file may see their documents, except the restricted types,
        // which follow the social-profile permission the rest of the sensitive data follows.

        [HttpPost("{id:int}/documents")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Edit)]
        public async Task<IActionResult> UploadDocument(int id, string typeCode, IFormFile? file, string? title, DateTime? expiry)
        {
            if (!await _db.Students.AnyAsync(s => s.Id == id, HttpContext.RequestAborted)) { return NotFound(); }

            try
            {
                await _intake.SaveAsync(
                    file, typeCode, StudentEntity, id,
                    titleAr: IsArabic ? title : null, titleEn: IsArabic ? null : title,
                    expiryDateUtc: expiry, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Document attached.", "تم إرفاق المستند.");
            }
            catch (FileRejectedException ex) { TempData["Error"] = Labels.FileRejection(ex.Rejection, IsArabic, ex.AllowedFormats, ex.MaxBytes); }
            catch (Sms.Application.Common.Exceptions.AttachmentPolicyViolationException) { TempData["Error"] = T("That file does not meet this document type's rules.", "هذا الملف لا يستوفي قواعد نوع المستند."); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(File), new { id, tab = "documents" });
        }

        [HttpGet("{id:int}/documents/{attachmentId:int}")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View)]
        public async Task<IActionResult> DownloadDocument(int id, int attachmentId)
        {
            // BR-ATT-005: the file is served only after this endpoint has satisfied itself that the
            // document belongs to the student in the route. Without the check, holding the file
            // permission for one student would read every attachment in the school by guessing ids.
            var owner = await _intake.OwnerOfAsync(attachmentId, HttpContext.RequestAborted);
            if (owner.OwningEntityType != StudentEntity || owner.OwningEntityId != id) { return NotFound(); }

            if (!await CanSeeRestrictedAsync() && await IsRestrictedDocumentAsync(attachmentId)) { return NotFound(); }

            var stored = await _intake.ReadAsync(attachmentId, HttpContext.RequestAborted);
            if (stored == null) { return NotFound(); }

            // Inline rather than as a download: doc 10 §7 asks a viewer to show a document without
            // making a copy of it first, and a browser can render every format this intake accepts.
            return File(stored.Content, stored.ContentType);
        }

        [HttpPost("{id:int}/documents/verify")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Approve)]
        public async Task<IActionResult> VerifyDocument(int id, int attachmentId)
        {
            var owner = await _intake.OwnerOfAsync(attachmentId, HttpContext.RequestAborted);
            if (owner.OwningEntityType != StudentEntity || owner.OwningEntityId != id) { return NotFound(); }

            try
            {
                await _intake.VerifyAsync(attachmentId, _currentUser.UserId, HttpContext.RequestAborted);
                TempData["Flash"] = T("Document marked as sighted.", "تم تأكيد مطابقة المستند.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(File), new { id, tab = "documents" });
        }

        [HttpPost("{id:int}/documents/void")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Deactivate)]
        public async Task<IActionResult> VoidDocument(int id, int attachmentId, string? reason)
        {
            var owner = await _intake.OwnerOfAsync(attachmentId, HttpContext.RequestAborted);
            if (owner.OwningEntityType != StudentEntity || owner.OwningEntityId != id) { return NotFound(); }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = T("Say why the document is being voided.", "اذكر سبب إلغاء المستند.");
                return RedirectToAction(nameof(File), new { id, tab = "documents" });
            }

            try
            {
                await _intake.VoidAsync(attachmentId, reason.Trim(), HttpContext.RequestAborted);
                TempData["Flash"] = T("Document voided.", "تم إلغاء المستند.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(File), new { id, tab = "documents" });
        }

        /// <summary>BR-GLB-072: the same permission that opens the social profile opens the restricted documents beside it.</summary>
        private Task<bool> CanSeeRestrictedAsync()
            => _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Students, ScreenCatalog.Students.SocialProfile, ActionVerb.View, HttpContext.RequestAborted);

        /// <summary>
        /// Read past the soft-active filter so a retired type still says it was restricted — but
        /// with the school put back by hand, because IgnoreQueryFilters drops the tenant filter too.
        /// </summary>
        private Task<bool> IsRestrictedDocumentAsync(int attachmentId)
            => _db.Attachments.AsNoTracking()
                .Where(a => a.Id == attachmentId)
                .Join(
                    _db.DocumentTypes.IgnoreQueryFilters().Where(t => t.SchoolId == _db.CurrentSchoolId),
                    a => a.DocumentTypeId, t => t.Id, (a, t) => t.IsRestricted)
                .SingleOrDefaultAsync(HttpContext.RequestAborted);

        [HttpPost("{id:int}/status")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Approve)]
        public async Task<IActionResult> Status(int id, StudentStatus target, string? reason)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                await _students.ChangeStatusAsync(id, target);
                TempData["Flash"] = T($"Status changed to {target}.", $"تغيرت الحالة إلى {target}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "status" });
        }

        [HttpPost("{id:int}/guardian")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Guardians, ActionVerb.Edit)]
        public async Task<IActionResult> LinkGuardian(int id, int? parentId, int? relationshipId, bool isPrimary, bool isFinancial, bool isPickup, bool isPortal, DateTime? effectiveFrom)
        {
            try
            {
                if (parentId == null || relationshipId == null) throw new InvalidOperationException(T("Parent and relationship are required.", "ولي الأمر وصلة القرابة مطلوبان."));
                await _students.LinkGuardianAsync(id, parentId.Value, relationshipId.Value, isPrimary, isFinancial, isPickup, isPortal, DateTime.SpecifyKind(effectiveFrom ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Guardian linked.", "تم ربط ولي الأمر.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "guardians" });
        }

        [HttpPost("{id:int}/guardian/{linkId:int}/unlink")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Guardians, ActionVerb.Deactivate)]
        public async Task<IActionResult> UnlinkGuardian(int id, int linkId, DateTime? effectiveTo)
        {
            try
            {
                await _students.UnlinkGuardianAsync(linkId, DateTime.SpecifyKind(effectiveTo ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Guardian link ended (history kept).", "تم إنهاء ربط ولي الأمر (مع حفظ السجل).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "guardians" });
        }

        [HttpPost("{id:int}/emergency")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Guardians, ActionVerb.Edit)]
        public async Task<IActionResult> AddEmergencyContact(int id, string? nameAr, string? nameEn, string? phone, bool isPickup, int? relationshipId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn) || string.IsNullOrWhiteSpace(phone)) throw new InvalidOperationException(T("Both names and a phone are required.", "الاسمان والهاتف مطلوبة."));
                await _students.AddEmergencyContactAsync(id, nameAr.Trim(), nameEn.Trim(), phone.Trim(), isPickup, relationshipId);
                TempData["Flash"] = T("Emergency contact added.", "تمت إضافة جهة اتصال الطوارئ.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "emergency" });
        }

        [HttpPost("{id:int}/enroll")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.Create)]
        /// <summary>
        /// One enrollment action, two screens. <paramref name="returnTo"/> sends the reader back
        /// where they started: the placement screen's next step is seating the child in a section,
        /// and bouncing them to the file's academic tab to get there would lose the thread.
        /// </summary>
        public async Task<IActionResult> Enroll(int id, int? gradeYearProfileId, DateTime? enrollmentDate, EnrollmentSourceType sourceType, string? returnTo = null)
        {
            try
            {
                if (gradeYearProfileId == null) throw new InvalidOperationException(T("Choose a grade-year.", "اختر الصف السنوي."));
                await _students.EnrollAsync(id, gradeYearProfileId.Value, enrollmentDate ?? _clock.UtcNow.Date, sourceType);
                TempData["Flash"] = T("Enrollment recorded.", "تم تسجيل القيد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return returnTo == "placement"
                ? RedirectToAction(nameof(Placement), new { id })
                : RedirectToAction(nameof(File), new { id, tab = "academic" });
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Deactivate)]
        public async Task<IActionResult> Delete(int id, string? q, StudentStatus? status, int? grade, int? section, Gender? gender)
        {
            var s = await _db.Students.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SchoolId == _db.CurrentSchoolId);
            if (s == null) return NotFound();
            try
            {
                await _students.DeleteStudentAsync(id);
                TempData["Flash"] = T($"Student {s.StudentNo} deleted.", $"تم حذف الطالب {s.StudentNo}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { q, status, grade, section, gender });
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
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var memberships = await _db.SectionMemberships.AsNoTracking().Where(m => enrollments.Select(e => e.Id).Contains(m.EnrollmentId) && m.EffectiveToUtc == null).ToListAsync();
            var sections = await _db.Sections.AsNoTracking().Where(x => memberships.Select(m => m.SectionId).Contains(x.Id)).ToListAsync();
            var audit = await _db.AuditEntries.AsNoTracking().Where(e => e.EntityType == nameof(Student) && e.EntityId == id).OrderByDescending(e => e.OccurredAtUtc).Take(100).ToListAsync();
            var allProfiles = await _db.GradeYearProfiles.AsNoTracking().ToListAsync();

            var model = new StudentFileViewModel
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
                EducationLevels = await LookupAsync("EducationLevel"),
            };

            // The residence picker's opening state and the one-line address beside it — see
            // StudentsController.Residence.cs.
            await FillResidenceAsync(model, s);

            return model;
        }


        private async Task<StudentFormViewModel> BuildFormAsync(Student? s)
        {
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
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
