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
using Sms.Application.Employees;
using Sms.Application.Teachers;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/12 §8 — E-203 (Employees, HR core): 8.1 directory,
    /// 8.2 Employee file (Personal ✎ T1 w/ reason · Position &amp; org ·
    /// Contracts 🔒 · Qualifications · Teaching · Status · Audit), 8.3 org
    /// chart editor, 8.6 contract manager (renewals pipeline + expiry
    /// console). Deferred with their engines: staff attendance console,
    /// leave (WF-10), training records, payroll-prep export, offboarding
    /// clearance wizard, documents (attachment screens). Salary 🔒 is shown
    /// here without the BR-GLB-072 permission gate (no role screens yet) —
    /// flagged, not hidden.
    /// </summary>
    [Route("employees")]
    public partial class EmployeesController : Controller
    {
        private readonly IEmployeeAdmin _employees;
        private readonly ITeacherAdmin _teachers;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;
        private readonly Sms.Web.Services.PersonPhotoService _photos;

        /// <summary>The one gate every file in the product passes through — the photograph above and the documents tab below use it alike.</summary>
        private readonly Sms.Web.Services.AttachmentIntake _intake;

        private readonly ICurrentUser _currentUser;

        /// <summary>What an attachment calls this record. Written once so a typo cannot detach a whole file's documents.</summary>
        private const string EmployeeEntity = "Employee";

        public EmployeesController(IEmployeeAdmin employees, ITeacherAdmin teachers, AppDbContext db, IAuditContext audit, IClock clock, Sms.Web.Services.PersonPhotoService photos, Sms.Web.Services.AttachmentIntake intake, ICurrentUser currentUser)
        {
            _intake = intake;
            _currentUser = currentUser;
            _employees = employees;
            _teachers = teachers;
            _db = db;
            _audit = audit;
            _clock = clock;
            _photos = photos;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ================================================================== 8.1 Directory

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.View)]
        public async Task<IActionResult> Index(string? q = null, EmployeeStatus? status = null, int? org = null, bool? teachers = null)
        {
            var query = _db.Employees.AsNoTracking().AsQueryable();
            if (status != null) query = query.Where(e => e.Status == status);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                // The mobile is searched as well as shown: a directory whose contact column cannot
                // answer "whose number is this?" makes the reader scroll three hundred rows.
                query = query.Where(e => e.EmployeeNo.Contains(t) || e.FirstNameAr.Contains(t) || e.FamilyNameAr.Contains(t) || e.FirstNameEn.Contains(t) || e.FamilyNameEn.Contains(t) || (e.PrimaryIdNo != null && e.PrimaryIdNo.Contains(t)) || (e.Mobile != null && e.Mobile.Contains(t)));
            }
            var total = await query.CountAsync();
            var employees = await query.OrderBy(e => e.EmployeeNo).Take(300).ToListAsync();
            var ids = employees.Select(e => e.Id).ToList();
            var current = await _db.EmployeeAssignments.AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.EffectiveToUtc == null).ToListAsync();
            var orgUnits = await _db.OrgUnits.AsNoTracking().OrderBy(u => u.NameEn).ToListAsync();
            var positions = await LookupAsync("JobTitle");
            var now = _clock.UtcNow;
            var contracts = await _db.Contracts.AsNoTracking().Where(c => ids.Contains(c.EmployeeId) && c.Status == ContractStatus.Active).ToListAsync();
            var teacherIds = await _db.TeacherProfiles.AsNoTracking().Where(p => ids.Contains(p.EmployeeId)).Select(p => p.EmployeeId).ToListAsync();

            var rows = employees.Select(e =>
            {
                var a = current.FirstOrDefault(x => x.EmployeeId == e.Id);
                var ou = a == null ? null : orgUnits.FirstOrDefault(u => u.Id == a.OrgUnitId);
                var pos = a == null ? null : positions.FirstOrDefault(p => p.Id == a.PositionLookupId) is var p && p != default ? (IsArabic ? p.Ar : p.En) : null;
                var c = contracts.Where(x => x.EmployeeId == e.Id && x.StartDate <= now && x.EndDate >= now).OrderByDescending(x => x.EndDate).FirstOrDefault();
                return new EmployeeListViewModel.Row(e, pos, ou == null ? null : (IsArabic ? ou.NameAr : ou.NameEn), c, teacherIds.Contains(e.Id));
            }).Where(r => (org == null || current.Any(a => a.EmployeeId == r.Employee.Id && a.OrgUnitId == org)) && (teachers != true || r.IsTeacher)).ToList();

            return View(new EmployeeListViewModel { Rows = rows, Query = q, Status = status, OrgUnitId = org, TeachersOnly = teachers, OrgUnits = orgUnits, Total = total });
        }

        // ================================================================== Register

        [HttpGet("new")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Register() => View(await BuildFormAsync());

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Register(EmployeeFormViewModel form)
        {
            try
            {
                RequireNames(form);
                if (form.DateOfBirth == null || form.NationalityLookupId == null) throw new InvalidOperationException(T("Date of birth and nationality are required.", "تاريخ الميلاد والجنسية مطلوبان."));

                // The photograph is judged before the employee exists, the way the student register
                // judges it: a file that is going to be refused must not leave a registered person
                // behind it. The second attempt at this form is how one human being ends up holding
                // two EMP numbers, which BR-EMP-001 exists to prevent.
                var rejection = form.Photo == null ? Sms.Web.Services.FileRejection.None : Sms.Web.Services.PersonPhotoService.Inspect(form.Photo);
                if (rejection != Sms.Web.Services.FileRejection.None) throw new InvalidOperationException(Labels.FileRejection(rejection, IsArabic, Sms.Web.Services.PersonPhotoService.PhotoFormats, Sms.Web.Services.PersonPhotoService.MaxPhotoBytes));

                var e = await _employees.RegisterEmployeeAsync(form.FirstNameAr!.Trim(), form.FatherNameAr!.Trim(), form.GrandfatherNameAr!.Trim(), form.FamilyNameAr!.Trim(), form.FirstNameEn!.Trim(), form.FatherNameEn!.Trim(), form.GrandfatherNameEn!.Trim(), form.FamilyNameEn!.Trim(),
                    form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, form.UserAccountId, form.PrimaryIdTypeLookupId, string.IsNullOrWhiteSpace(form.PrimaryIdNo) ? null : form.PrimaryIdNo.Trim(), form.PrimaryIdExpiry,
                    form.Mobile, form.WhatsAppNumber);

                if (form.Photo != null)
                {
                    // Inspect() has already passed, so what is left is the document type's own
                    // upload policy, which a school may have tightened. The employee is registered
                    // either way: an EMP number is permanent and never re-issued, so undoing a
                    // registration over a refused JPEG costs more than the photograph is worth.
                    try
                    {
                        await AttachPhotoAsync(e.Id, form.Photo);
                    }
                    catch (Sms.Application.Common.Exceptions.AttachmentPolicyViolationException)
                    {
                        TempData["Error"] = T("The employee was registered, but the photo was not accepted — add it from the employee file.", "تم تسجيل الموظف، لكن الصورة لم تُقبل — أضفها من ملف الموظف.");
                    }
                }

                if (form.OrgUnitId != null && form.PositionLookupId != null)
                {
                    await _employees.AssignPositionAsync(e.Id, form.OrgUnitId.Value, form.PositionLookupId.Value, form.ManagerEmployeeId, _clock.UtcNow.Date);
                }
                TempData["Flash"] = T($"Employee {e.EmployeeNo} registered.", $"تم تسجيل الموظف {e.EmployeeNo}.");
                return RedirectToAction(nameof(File), new { id = e.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                var model = await BuildFormAsync();
                Copy(form, model);
                return View(model);
            }
        }

        // ================================================================== 8.2 Employee file

        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.View)]
        public async Task<IActionResult> File(int id, string? tab = null)
        {
            var m = await BuildFileAsync(id);
            if (m == null) return NotFound();
            m.ActiveTab = tab ?? "personal";

            // BR-ATT-004: documents inherit this screen's access. The restricted employee types
            // (contract, medical fitness) are not withheld separately yet — the module doc's 🔒
            // gate waits on the same role screens that leave salary visible here, and hiding them
            // from everyone would be a worse answer than the flag this file already carries.
            m.Documents = new EntityDocumentsViewModel
            {
                Controller = "Employees",
                OwnerId = id,
                OwnerName = IsArabic ? m.Employee.FirstNameAr : m.Employee.FirstNameEn,
                Rows = await _intake.ListAsync(EmployeeEntity, id, canSeeRestricted: true, HttpContext.RequestAborted),
                Types = await _intake.TypesForAsync(ScreenCatalog.Modules.Employees, includeRestricted: true, HttpContext.RequestAborted),
                CanEdit = true,
                CanVerify = true,
            };

            return View(m);
        }

        // ------------------------------------------------------------------ photograph
        //
        // The same slot the student file uses, on the same pipeline: one photo per person, replaced
        // by re-uploading rather than accumulating, and served from its own action so a browser can
        // cache it instead of receiving it inside every page.

        [HttpGet("{id:int}/photo")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.View)]
        public async Task<IActionResult> Photo(int id)
        {
            var photoId = await _db.Employees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.Id == id && e.SchoolId == _db.CurrentSchoolId)
                .Select(e => e.PhotoAttachmentId)
                .SingleOrDefaultAsync();

            var photo = await _photos.ReadAsync(photoId, HttpContext.RequestAborted);
            if (photo == null) { return NotFound(); }

            return File(photo.Content, photo.ContentType);
        }

        [HttpPost("{id:int}/photo")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> UploadPhoto(int id, IFormFile? photo)
        {
            try
            {
                var employee = await _db.Employees.SingleOrDefaultAsync(e => e.Id == id);
                if (employee == null) return NotFound();

                employee.PhotoAttachmentId = await _photos.SaveAsync(
                    photo!, "Employee", id, ScreenCatalog.Modules.Employees, HttpContext.RequestAborted);
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
                TempData["Flash"] = T("Photo updated.", "تم تحديث الصورة.");
            }
            // Specific first: the policy exception derives from InvalidOperationException, and its
            // own message names a rule where the uploader needs a plain fact.
            catch (Sms.Application.Common.Exceptions.AttachmentPolicyViolationException)
            {
                TempData["Error"] = T("That file is not an acceptable photo.", "هذا الملف ليس صورة مقبولة.");
            }
            // Also an InvalidOperationException, and it carries a reason rather than a sentence —
            // the wording is chosen here, in the reader's language, never thrown from the service.
            catch (Sms.Web.Services.FileRejectedException ex) { TempData["Error"] = Labels.FileRejection(ex.Rejection, IsArabic, ex.AllowedFormats, ex.MaxBytes); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        /// <summary>
        /// Points the employee's row at the photograph once the person exists — nothing can be
        /// stored against an EMP id that has not been issued yet, which is why the registration
        /// form calls this after the record is made rather than sending the file on its own.
        /// </summary>
        private async Task AttachPhotoAsync(int employeeId, IFormFile file)
        {
            var attachmentId = await _photos.SaveAsync(
                file, "Employee", employeeId, ScreenCatalog.Modules.Employees, HttpContext.RequestAborted);

            var employee = await _db.Employees.SingleAsync(e => e.Id == employeeId, HttpContext.RequestAborted);
            employee.PhotoAttachmentId = attachmentId;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
        }

        [HttpPost("{id:int}/photo/remove")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> RemovePhoto(int id)
        {
            var employee = await _db.Employees.SingleOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            // Pointer cleared, file kept: doc 10 does not delete while the owning record lives.
            employee.PhotoAttachmentId = null;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            TempData["Flash"] = T("Photo removed.", "تمت إزالة الصورة.");
            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        // ------------------------------------------------------------------ documents
        //
        // doc 10 §5 "Entity documents tab", and doc/Modules/12 §8.2's deferred "documents
        // (attachment screens)". Contracts, qualifications, ID papers and medical fitness are the
        // types the module doc lists; which of them a school defines is Setup's business, not this
        // screen's. The four verbs and the partial are the student file's — one mechanism, learned
        // once, so a contract and a birth certificate are filed the same way.

        [HttpPost("{id:int}/documents")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> UploadDocument(int id, string typeCode, IFormFile? file, string? title, DateTime? expiry)
        {
            if (!await _db.Employees.AnyAsync(e => e.Id == id, HttpContext.RequestAborted)) { return NotFound(); }

            try
            {
                await _intake.SaveAsync(
                    file, typeCode, EmployeeEntity, id,
                    titleAr: IsArabic ? title : null, titleEn: IsArabic ? null : title,
                    expiryDateUtc: expiry, cancellationToken: HttpContext.RequestAborted);
                TempData["Flash"] = T("Document attached.", "تم إرفاق المستند.");
            }
            catch (Sms.Web.Services.FileRejectedException ex) { TempData["Error"] = Labels.FileRejection(ex.Rejection, IsArabic, ex.AllowedFormats, ex.MaxBytes); }
            catch (Sms.Application.Common.Exceptions.AttachmentPolicyViolationException) { TempData["Error"] = T("That file does not meet this document type's rules.", "هذا الملف لا يستوفي قواعد نوع المستند."); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return RedirectToAction(nameof(File), new { id, tab = "documents" });
        }

        [HttpGet("{id:int}/documents/{attachmentId:int}")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.View)]
        public async Task<IActionResult> DownloadDocument(int id, int attachmentId)
        {
            // BR-ATT-005: the document has to belong to the employee in the route. Without the
            // check, one employee's file permission would read every attachment in the school by
            // guessing ids.
            var owner = await _intake.OwnerOfAsync(attachmentId, HttpContext.RequestAborted);
            if (owner.OwningEntityType != EmployeeEntity || owner.OwningEntityId != id) { return NotFound(); }

            var stored = await _intake.ReadAsync(attachmentId, HttpContext.RequestAborted);
            if (stored == null) { return NotFound(); }

            return File(stored.Content, stored.ContentType);
        }

        [HttpPost("{id:int}/documents/verify")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Approve)]
        public async Task<IActionResult> VerifyDocument(int id, int attachmentId)
        {
            var owner = await _intake.OwnerOfAsync(attachmentId, HttpContext.RequestAborted);
            if (owner.OwningEntityType != EmployeeEntity || owner.OwningEntityId != id) { return NotFound(); }

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
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Deactivate)]
        public async Task<IActionResult> VoidDocument(int id, int attachmentId, string? reason)
        {
            var owner = await _intake.OwnerOfAsync(attachmentId, HttpContext.RequestAborted);
            if (owner.OwningEntityType != EmployeeEntity || owner.OwningEntityId != id) { return NotFound(); }

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

        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> Edit(int id, EmployeeFormViewModel form)
        {
            try
            {
                RequireNames(form);
                if (form.DateOfBirth == null || form.NationalityLookupId == null) throw new InvalidOperationException(T("Date of birth and nationality are required.", "تاريخ الميلاد والجنسية مطلوبان."));
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason.Trim();
                await _employees.UpdateEmployeeAsync(id, form.FirstNameAr!.Trim(), form.FatherNameAr!.Trim(), form.GrandfatherNameAr!.Trim(), form.FamilyNameAr!.Trim(), form.FirstNameEn!.Trim(), form.FatherNameEn!.Trim(), form.GrandfatherNameEn!.Trim(), form.FamilyNameEn!.Trim(),
                    form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, form.UserAccountId, form.PrimaryIdTypeLookupId, string.IsNullOrWhiteSpace(form.PrimaryIdNo) ? null : form.PrimaryIdNo.Trim(), form.PrimaryIdExpiry,
                    form.Mobile, form.WhatsAppNumber);
                TempData["Flash"] = T("Employee file updated.", "تم تحديث ملف الموظف.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        /// <summary>
        /// The personal block below the identity form — marital status and the spouse's document,
        /// the address and the town of origin, and the three places the employee's pay can be sent
        /// (owner request 2026-08-27, extending 2026-08-23's bank pair). None of it is in
        /// doc/Modules/12 §7; it is recorded because the school's own staff register records it.
        /// <para>
        /// A second form on one tab rather than a longer first one: this half needs no reason
        /// unless the marital status or a payment destination actually moves, and merging them
        /// would put a mandatory-reason box in front of someone correcting a street name.
        /// </para>
        /// <para>
        /// <paramref name="bankLookupId"/> is the "Bank" catalogue value the picker offers;
        /// <paramref name="bankName"/> is the free text the field held before it, still shown as a
        /// text box on the rows that carry one so that saving an address neither discards the bank
        /// of an employee nobody has catalogued yet nor makes it uncorrectable. The service keeps
        /// one or the other, never both.
        /// </para>
        /// </summary>
        [HttpPost("{id:int}/personal")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> PersonalDetails(int id, MaritalStatus? maritalStatus, int? spouseIdTypeLookupId, string? spouseIdNo, string? address, string? originTown, int? bankLookupId, string? bankName, string? bankAccountNo, string? palPayWalletNo, string? jawwalPayWalletNo, string? reason)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await _employees.UpdatePersonalDetailsAsync(id, maritalStatus, bankLookupId, bankName, bankAccountNo, address, originTown, spouseIdTypeLookupId, spouseIdNo, palPayWalletNo, jawwalPayWalletNo);
                TempData["Flash"] = T("Personal details updated.", "تم تحديث البيانات الشخصية.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "personal" });
        }

        [HttpPost("{id:int}/status")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Approve)]
        public async Task<IActionResult> Status(int id, EmployeeStatus target, string? reason)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await _employees.ChangeStatusAsync(id, target);
                TempData["Flash"] = T($"Status changed to {StaffLabels.EmployeeStatus(target, false)}.", $"تغيرت الحالة إلى {StaffLabels.EmployeeStatus(target, true)}.");
                if (target == EmployeeStatus.Terminated) TempData["Flash"] += " " + T("Offboarding: the linked user account should be deactivated (BR-EMP-001/008) — account admin lands with Module 36 screens.", "إنهاء الخدمة: يجب تعطيل حساب المستخدم المرتبط (BR-EMP-001/008) — إدارة الحسابات مع شاشات الوحدة 36.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "status" });
        }

        [HttpPost("{id:int}/position")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> AssignPosition(int id, int? orgUnitId, int? positionLookupId, int? managerEmployeeId, DateTime? effectiveFrom)
        {
            try
            {
                if (orgUnitId == null || positionLookupId == null) throw new InvalidOperationException(T("Org unit and position are required (BR-EMP-002).", "الوحدة التنظيمية والمسمى الوظيفي مطلوبان (BR-EMP-002)."));
                if (managerEmployeeId == id) throw new InvalidOperationException(T("An employee cannot report to themselves.", "لا يمكن أن يكون الموظف مديراً لنفسه."));
                await _employees.AssignPositionAsync(id, orgUnitId.Value, positionLookupId.Value, managerEmployeeId, DateTime.SpecifyKind(effectiveFrom ?? _clock.UtcNow.Date, DateTimeKind.Utc));
                TempData["Flash"] = T("Position assigned — the previous assignment was closed (history kept).", "تم إسناد المنصب — أُغلق الإسناد السابق مع حفظ السجل.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "position" });
        }

        [HttpPost("{id:int}/contracts")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, ActionVerb.Create)]
        public async Task<IActionResult> DefineContract(int id, ContractType type, DateTime? startDate, DateTime? endDate, decimal? salaryBasic, decimal? salaryAllowances)
        {
            try
            {
                ValidateContract(startDate, endDate, salaryBasic);
                await _employees.DefineContractAsync(id, type, startDate!.Value, endDate!.Value, salaryBasic!.Value, salaryAllowances);
                TempData["Flash"] = T("Contract drafted — activate it once approved.", "أُنشئت مسودة العقد — فعّلها بعد الاعتماد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "contracts" });
        }

        [HttpPost("{id:int}/contracts/{contractId:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, ActionVerb.Edit)]
        public async Task<IActionResult> EditContract(int id, int contractId, ContractType type, DateTime? startDate, DateTime? endDate, decimal? salaryBasic, decimal? salaryAllowances, string? reason, string? returnTo)
        {
            try
            {
                ValidateContract(startDate, endDate, salaryBasic);
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await _employees.UpdateContractAsync(contractId, type, startDate!.Value, endDate!.Value, salaryBasic!.Value, salaryAllowances);
                TempData["Flash"] = T("Contract updated.", "حُدّث العقد.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return returnTo == "manager" ? RedirectToAction(nameof(Contracts)) : RedirectToAction(nameof(File), new { id, tab = "contracts" });
        }

        [HttpPost("{id:int}/contracts/{contractId:int}/status")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, ActionVerb.Approve)]
        public async Task<IActionResult> ContractStatusChange(int id, int contractId, ContractStatus target, string? reason, string? returnTo)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await _employees.ChangeContractStatusAsync(contractId, target);
                TempData["Flash"] = T($"Contract is now {StaffLabels.ContractStatus(target, false)}.", $"أصبح العقد {StaffLabels.ContractStatus(target, true)}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return returnTo == "manager" ? RedirectToAction(nameof(Contracts)) : RedirectToAction(nameof(File), new { id, tab = "contracts" });
        }

        /// <summary>
        /// BR-EMP-004. The qualification, university, specialization and classification are chosen
        /// from catalogues (owner request 2026-08-27); the written titles stay for the licences and
        /// certificates no catalogue names, and one of the two has to be given.
        /// </summary>
        [HttpPost("{id:int}/qualifications")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> AddQualification(int id, string? titleAr, string? titleEn, DateTime? dateAwarded, bool isTeachingRelevant, string? institution, int? educationLookupId, int? universityLookupId, int? specializationLookupId, int? academicGradeLookupId, string? gpa)
        {
            try
            {
                var average = ParseGpa(gpa);
                ValidateQualification(titleAr, titleEn, dateAwarded, educationLookupId, average);
                await _employees.AddQualificationAsync(id, (titleAr ?? string.Empty).Trim(), (titleEn ?? string.Empty).Trim(), dateAwarded!.Value, isTeachingRelevant, string.IsNullOrWhiteSpace(institution) ? null : institution.Trim(),
                    documentAttachmentId: null, educationLookupId: educationLookupId, universityLookupId: universityLookupId, specializationLookupId: specializationLookupId, academicGradeLookupId: academicGradeLookupId, gpa: average);
                TempData["Flash"] = T("Qualification added.", "أُضيف المؤهل.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "qualifications" });
        }

        /// <summary>
        /// Corrects a qualification already on the file. Six fields picked from four dropdowns is
        /// six chances to pick the wrong row, and BR-GLB-005 leaves no delete to undo it with.
        /// </summary>
        [HttpPost("{id:int}/qualifications/{qualificationId:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> EditQualification(int id, int qualificationId, string? titleAr, string? titleEn, DateTime? dateAwarded, bool isTeachingRelevant, string? institution, int? educationLookupId, int? universityLookupId, int? specializationLookupId, int? academicGradeLookupId, string? gpa)
        {
            // The route's employee owns the row, or there is no row: without the check, one file's
            // edit permission would rewrite any qualification in the school by guessing an id.
            if (!await _db.Qualifications.AnyAsync(q => q.Id == qualificationId && q.EmployeeId == id, HttpContext.RequestAborted)) { return NotFound(); }

            try
            {
                var average = ParseGpa(gpa);
                ValidateQualification(titleAr, titleEn, dateAwarded, educationLookupId, average);
                await _employees.UpdateQualificationAsync(qualificationId, (titleAr ?? string.Empty).Trim(), (titleEn ?? string.Empty).Trim(), dateAwarded!.Value, isTeachingRelevant, string.IsNullOrWhiteSpace(institution) ? null : institution.Trim(),
                    educationLookupId, universityLookupId, specializationLookupId, academicGradeLookupId, average);
                TempData["Flash"] = T("Qualification updated.", "حُدّث المؤهل.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "qualifications" });
        }

        /// <summary>
        /// Takes the GPA off the form as text and hands it to
        /// <see cref="GradePointAverageReader"/>, which knows that this screen runs under two
        /// cultures and that the number control only ever posts one of them. Bound as a string
        /// rather than a <c>decimal?</c> on purpose — see that class for the defect it exists to
        /// prevent. The refusal is worded here, in the reader's language, as every refusal is.
        /// </summary>
        private static decimal? ParseGpa(string? raw)
        {
            if (!GradePointAverageReader.TryRead(raw, CultureInfo.CurrentCulture, out var value))
            {
                throw new InvalidOperationException(T("The GPA is not a number.", "المعدل ليس رقماً."));
            }

            return value;
        }

        /// <summary>
        /// Refuses in the reader's language before the engine can refuse in English — the identity
        /// rule is repeated here rather than left to <c>EmployeeAdmin</c> because the message a
        /// registrar reads has to name the two ways out of it, not the rule number.
        /// </summary>
        private static void ValidateQualification(string? titleAr, string? titleEn, DateTime? dateAwarded, int? educationLookupId, decimal? gpa)
        {
            if (educationLookupId == null && (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn)))
            {
                throw new InvalidOperationException(T("Choose a qualification, or write both titles (BR-EMP-004).", "اختر المؤهل، أو اكتب العنوانين (BR-EMP-004)."));
            }

            if (dateAwarded == null) throw new InvalidOperationException(T("The graduation date is required.", "تاريخ التخرج مطلوب."));

            // The certificate states it out of 4 or out of 100 and this system does not convert
            // between them, so the only bound worth enforcing is the column's own.
            if (!GradePointAverageReader.IsInRange(gpa)) throw new InvalidOperationException(T("The GPA must be between 0 and 100.", "المعدل يجب أن يكون بين 0 و100."));
        }

        [HttpPost("{id:int}/teaching/designate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> Designate(int id, int? maxWeeklyPeriods)
        {
            try
            {
                if (maxWeeklyPeriods == null || maxWeeklyPeriods <= 0) throw new InvalidOperationException(T("Max weekly periods must be positive (BR-TCH-004).", "الحد الأقصى للحصص الأسبوعية يجب أن يكون موجباً (BR-TCH-004)."));
                if (await _db.TeacherProfiles.AnyAsync(p => p.EmployeeId == id)) throw new InvalidOperationException(T("Already designated as a teacher.", "مُعيَّن معلماً مسبقاً."));
                await _teachers.DesignateTeacherAsync(id, maxWeeklyPeriods.Value);
                TempData["Flash"] = T("Designated as a teacher (BR-TCH-001).", "عُيِّن معلماً (BR-TCH-001).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "teaching" });
        }

        [HttpPost("{id:int}/teaching/maxload")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> MaxLoad(int id, int profileId, int? maxWeeklyPeriods)
        {
            try
            {
                if (maxWeeklyPeriods == null || maxWeeklyPeriods <= 0) throw new InvalidOperationException(T("Max weekly periods must be positive.", "الحد الأقصى يجب أن يكون موجباً."));
                await _teachers.UpdateMaxLoadAsync(profileId, maxWeeklyPeriods.Value);
                TempData["Flash"] = T("Max load updated.", "حُدّث الحد الأقصى للنصاب.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(File), new { id, tab = "teaching" });
        }

        // ================================================================== 8.3 Org chart

        [HttpGet("org")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.OrgChart, ActionVerb.View)]
        public async Task<IActionResult> Org()
        {
            var units = await _db.OrgUnits.AsNoTracking().OrderBy(u => u.NameEn).ToListAsync();
            var current = await _db.EmployeeAssignments.AsNoTracking().Where(a => a.EffectiveToUtc == null).ToListAsync();
            var empIds = current.Select(a => a.EmployeeId).Distinct().ToList();
            var employees = await _db.Employees.AsNoTracking().Where(e => empIds.Contains(e.Id) && e.Status != EmployeeStatus.Terminated).ToListAsync();
            var positions = await LookupAsync("JobTitle");
            var nodes = new List<OrgChartViewModel.Node>();
            // One roster builder for both passes. The orphan pass below used to count its heads
            // but hand back an empty list — invisible while the roster was a truncated caption,
            // but the screen now opens that list behind a button, and a unit the head-count
            // column calls three-strong may not open on nobody.
            List<(Employee, string?)> Members(int unitId) => current
                .Where(a => a.OrgUnitId == unitId)
                .Select(a => (employees.FirstOrDefault(e => e.Id == a.EmployeeId), positions.FirstOrDefault(p => p.Id == a.PositionLookupId) is var p && p != default ? (IsArabic ? p.Ar : p.En) : null))
                .Where(x => x.Item1 != null).Select(x => (x.Item1!, x.Item2)).ToList();
            void Walk(int? parentId, int depth)
            {
                foreach (var u in units.Where(x => x.ParentOrgUnitId == parentId))
                {
                    var members = Members(u.Id);
                    nodes.Add(new OrgChartViewModel.Node(u, depth, members.Count, units.Count(x => x.ParentOrgUnitId == u.Id), members));
                    if (depth < 12) Walk(u.Id, depth + 1);
                }
            }
            Walk(null, 0);
            // orphans whose parent no longer exists still show, at root level
            foreach (var u in units.Where(u => u.ParentOrgUnitId != null && units.All(x => x.Id != u.ParentOrgUnitId) && nodes.All(n => n.Unit.Id != u.Id)))
            {
                var members = Members(u.Id);
                nodes.Add(new OrgChartViewModel.Node(u, 0, members.Count, 0, members));
            }
            var unassigned = await _db.Employees.AsNoTracking().CountAsync(e => e.Status != EmployeeStatus.Terminated && !empIds.Contains(e.Id));
            return View(new OrgChartViewModel { Nodes = nodes, All = units, Unassigned = unassigned });
        }

        [HttpPost("org/new")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.OrgChart, ActionVerb.Create)]
        public async Task<IActionResult> CreateOrgUnit(string? nameAr, string? nameEn, int? parentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Both names are required (BR-GLB-001).", "الاسمان مطلوبان (BR-GLB-001)."));
                await _employees.DefineOrgUnitAsync(nameAr.Trim(), nameEn.Trim(), parentId);
                TempData["Flash"] = T("Org unit added.", "أُضيفت الوحدة التنظيمية.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Org));
        }

        [HttpPost("org/{unitId:int}/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.OrgChart, ActionVerb.Edit)]
        public async Task<IActionResult> EditOrgUnit(int unitId, string? nameAr, string? nameEn, int? parentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn)) throw new InvalidOperationException(T("Both names are required.", "الاسمان مطلوبان."));
                await _employees.UpdateOrgUnitAsync(unitId, nameAr.Trim(), nameEn.Trim(), parentId);
                TempData["Flash"] = T("Org unit updated.", "حُدّثت الوحدة التنظيمية.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Org));
        }

        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Deactivate)]
        public async Task<IActionResult> Delete(int id, string? q, EmployeeStatus? status, int? org, bool? teachers)
        {
            var e = await _db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (e == null) return NotFound();
            try
            {
                await _employees.DeleteEmployeeAsync(id);
                TempData["Flash"] = T($"Employee {e.EmployeeNo} deleted.", $"تم حذف الموظف {e.EmployeeNo}.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Index), new { q, status, org, teachers });
        }

        [HttpPost("org/{unitId:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.OrgChart, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteOrgUnit(int unitId)
        {
            try { await _employees.DeleteOrgUnitAsync(unitId); TempData["Flash"] = T("Org unit deleted.", "حُذفت الوحدة التنظيمية."); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Org));
        }

        // ================================================================== 8.6 Contract manager 🔒

        [HttpGet("contracts")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Contracts, ActionVerb.View)]
        public async Task<IActionResult> Contracts(int window = 90)
        {
            var now = _clock.UtcNow.Date;
            var contracts = await _db.Contracts.AsNoTracking().ToListAsync();
            var empIds = contracts.Select(c => c.EmployeeId).Distinct().ToList();
            var employees = await _db.Employees.AsNoTracking().Where(e => empIds.Contains(e.Id)).ToListAsync();
            ContractManagerViewModel.Row R(Contract c)
            {
                var e = employees.First(x => x.Id == c.EmployeeId);
                var successor = contracts.Any(x => x.EmployeeId == c.EmployeeId && x.Id != c.Id && x.Status != ContractStatus.Terminated && x.StartDate > c.EndDate);
                return new ContractManagerViewModel.Row(c, e, (int)(c.EndDate.Date - now).TotalDays, c.EndDate.Date < now, successor);
            }
            var active = contracts.Where(c => c.Status == ContractStatus.Active).ToList();
            return View(new ContractManagerViewModel
            {
                WindowDays = window,
                Drafts = contracts.Where(c => c.Status == ContractStatus.Draft).OrderBy(c => c.StartDate).Select(R).ToList(),
                Expiring = active.Where(c => c.EndDate.Date >= now && (c.EndDate.Date - now).TotalDays <= window).OrderBy(c => c.EndDate).Select(R).ToList(),
                Active = active.Where(c => c.EndDate.Date >= now && (c.EndDate.Date - now).TotalDays > window).OrderBy(c => c.EndDate).Select(R).ToList(),
                Expired = active.Where(c => c.EndDate.Date < now).OrderByDescending(c => c.EndDate).Select(R).ToList(),
            });
        }

        // ================================================================== helpers

        private async Task<EmployeeFileViewModel?> BuildFileAsync(int id)
        {
            var e = await _db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (e == null) return null;
            var nats = await LookupAsync("Nationality"); var idTypes = await LookupAsync("IdType"); var positions = await LookupAsync("JobTitle");

            // The qualifications tab's four catalogues, twice over and for different questions:
            // the filtered lists fill the pickers, and `names` — read past the soft-active filter —
            // resolves what is already recorded. Without the second read, retiring a university
            // would blank the degree of everyone who holds one from it.
            var educationLevels = await LookupAsync("EducationLevel");
            var universities = await LookupAsync("University");
            var specializations = await LookupAsync("Specialization");
            var academicGrades = await LookupAsync("AcademicGrade");

            // The personal tab's bank joins the same two reads (owner request 2026-08-30). It is on
            // this list rather than beside the qualifications because it is the same question:
            // offer the active values, resolve the stored one whatever its state.
            var banks = await LookupAsync("Bank");
            var names = await LookupNamesAsync("EducationLevel", "University", "Specialization", "AcademicGrade", "Bank");
            string? Named(int? lookupId) => lookupId != null && names.TryGetValue(lookupId.Value, out var v) ? (IsArabic ? v.Ar : v.En) : null;

            var orgUnits = await _db.OrgUnits.AsNoTracking().OrderBy(u => u.NameEn).ToListAsync();
            var assignments = await _db.EmployeeAssignments.AsNoTracking().Where(a => a.EmployeeId == id).OrderByDescending(a => a.EffectiveFromUtc).ToListAsync();
            var managerIds = assignments.Where(a => a.ManagerEmployeeId != null).Select(a => a.ManagerEmployeeId!.Value).Distinct().ToList();
            var managers = await _db.Employees.AsNoTracking().Where(x => managerIds.Contains(x.Id)).ToListAsync();
            var contracts = await _db.Contracts.AsNoTracking().Where(c => c.EmployeeId == id).OrderByDescending(c => c.StartDate).ToListAsync();
            var now = _clock.UtcNow;
            var profile = await _db.TeacherProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.EmployeeId == id);
            var teaching = new List<EmployeeFileViewModel.TeachingRow>(); var load = 0;
            if (profile != null)
            {
                var tas = await _db.TeacherAssignments.AsNoTracking().Where(a => a.TeacherProfileId == profile.Id).OrderByDescending(a => a.EffectiveFromUtc).ToListAsync();
                var offs = await _db.CurriculumOfferings.AsNoTracking().Where(o => tas.Select(a => a.CurriculumOfferingId).Contains(o.Id)).ToListAsync();
                var subs = await _db.Subjects.IgnoreQueryFilters().AsNoTracking().Where(s => offs.Select(o => o.SubjectId).Contains(s.Id)).ToListAsync();
                var secs = await _db.Sections.AsNoTracking().Where(s => tas.Select(a => a.SectionId).Contains(s.Id)).ToListAsync();
                teaching = tas.Select(a => { var o = offs.FirstOrDefault(x => x.Id == a.CurriculumOfferingId); return new EmployeeFileViewModel.TeachingRow(a, o == null ? null : subs.FirstOrDefault(s => s.Id == o.SubjectId), secs.FirstOrDefault(s => s.Id == a.SectionId), o); }).ToList();
                load = TeacherLoadCalculator.CurrentLoad(teaching.Where(t => t.Assignment.EffectiveToUtc == null && t.Offering != null).Select(t => t.Offering!.WeeklyPeriods).ToArray());
            }
            var contractIds = contracts.Select(c => (long?)c.Id).ToList();
            var audit = await _db.AuditEntries.AsNoTracking().Where(a => (a.EntityType == nameof(Employee) && a.EntityId == id) || (a.EntityType == nameof(Contract) && contractIds.Contains(a.EntityId))).OrderByDescending(a => a.OccurredAtUtc).Take(100).ToListAsync();
            return new EmployeeFileViewModel
            {
                Employee = e,
                NationalityName = nats.FirstOrDefault(n => n.Id == e.NationalityLookupId) is var n && n != default ? (IsArabic ? n.Ar : n.En) : "?",
                IdTypeName = e.PrimaryIdTypeLookupId == null ? null : (idTypes.FirstOrDefault(t => t.Id == e.PrimaryIdTypeLookupId) is var t && t != default ? (IsArabic ? t.Ar : t.En) : "?"),
                Assignments = assignments.Select(a => new EmployeeFileViewModel.AssignmentRow(a, orgUnits.FirstOrDefault(u => u.Id == a.OrgUnitId), positions.FirstOrDefault(p => p.Id == a.PositionLookupId) is var p && p != default ? (IsArabic ? p.Ar : p.En) : null, managers.FirstOrDefault(m => m.Id == a.ManagerEmployeeId))).ToList(),
                Contracts = contracts,
                Qualifications = (await _db.Qualifications.AsNoTracking().Where(q => q.EmployeeId == id).OrderByDescending(q => q.DateAwarded).ToListAsync())
                    .Select(q => new EmployeeFileViewModel.QualificationRow(q, Named(q.EducationLookupId), Named(q.UniversityLookupId), Named(q.SpecializationLookupId), Named(q.AcademicGradeLookupId))).ToList(),
                TeacherProfile = profile, Teaching = teaching, CurrentLoad = load,
                HasActiveContract = contracts.Any(c => c.Status == ContractStatus.Active && c.StartDate <= now && c.EndDate >= now),
                AllowedTransitions = Enum.GetValues<EmployeeStatus>().Where(s => EmployeeStatusTransitions.CanTransition(e.Status, s)).ToList(),
                Audit = audit.Select(a => (Action: a.Action.ToString(), Field: a.FieldName, Old: a.OldValue, New: a.NewValue, At: a.OccurredAtUtc, Actor: a.ActorUserId, Reason: a.Reason)).ToList(),
                Nationalities = nats, IdTypes = idTypes, Positions = positions, OrgUnits = orgUnits,
                EducationLevels = educationLevels, Universities = universities, Specializations = specializations, AcademicGrades = academicGrades,
                Banks = banks,

                // The catalogue name if the row points at one, and otherwise the free text a
                // register entered before the picker existed — never both, and never neither
                // silently: EmployeeAdmin clears one when the other is set.
                BankName = Named(e.BankLookupId) ?? e.BankName,
                Managers = await _db.Employees.AsNoTracking().Where(x => x.Id != id && x.Status == EmployeeStatus.Active).OrderBy(x => x.EmployeeNo).Take(500).ToListAsync(),
            };
        }

        private async Task<EmployeeFormViewModel> BuildFormAsync() => new()
        {
            Nationalities = await LookupAsync("Nationality"), IdTypes = await LookupAsync("IdType"), Positions = await LookupAsync("JobTitle"),
            OrgUnits = await _db.OrgUnits.AsNoTracking().OrderBy(u => u.NameEn).ToListAsync(),
            Managers = await _db.Employees.AsNoTracking().Where(x => x.Status == EmployeeStatus.Active).OrderBy(x => x.EmployeeNo).Take(500).ToListAsync(),
        };

        private static void Copy(EmployeeFormViewModel from, EmployeeFormViewModel to)
        {
            to.FirstNameAr = from.FirstNameAr; to.FatherNameAr = from.FatherNameAr; to.GrandfatherNameAr = from.GrandfatherNameAr; to.FamilyNameAr = from.FamilyNameAr;
            to.FirstNameEn = from.FirstNameEn; to.FatherNameEn = from.FatherNameEn; to.GrandfatherNameEn = from.GrandfatherNameEn; to.FamilyNameEn = from.FamilyNameEn;
            to.Gender = from.Gender; to.DateOfBirth = from.DateOfBirth; to.NationalityLookupId = from.NationalityLookupId; to.PrimaryIdTypeLookupId = from.PrimaryIdTypeLookupId; to.PrimaryIdNo = from.PrimaryIdNo; to.PrimaryIdExpiry = from.PrimaryIdExpiry;
            to.OrgUnitId = from.OrgUnitId; to.PositionLookupId = from.PositionLookupId; to.ManagerEmployeeId = from.ManagerEmployeeId; to.UserAccountId = from.UserAccountId; to.Mobile = from.Mobile; to.WhatsAppNumber = from.WhatsAppNumber;
        }

        private static void RequireNames(EmployeeFormViewModel f)
        {
            foreach (var (v, n) in new[] { (f.FirstNameAr, "First name (Arabic)"), (f.FatherNameAr, "Father name (Arabic)"), (f.GrandfatherNameAr, "Grandfather name (Arabic)"), (f.FamilyNameAr, "Family name (Arabic)"), (f.FirstNameEn, "First name (English)"), (f.FatherNameEn, "Father name (English)"), (f.GrandfatherNameEn, "Grandfather name (English)"), (f.FamilyNameEn, "Family name (English)") })
            {
                if (string.IsNullOrWhiteSpace(v)) throw new InvalidOperationException(T($"{n} is required (BR-GLB-001).", $"الحقل {n} مطلوب (BR-GLB-001)."));
            }
        }

        private static void ValidateContract(DateTime? start, DateTime? end, decimal? basic)
        {
            if (start == null || end == null || basic == null) throw new InvalidOperationException(T("Start, end and basic salary are required.", "البداية والنهاية والراتب الأساسي مطلوبة."));
            if (end < start) throw new InvalidOperationException(T("End date must not precede the start date.", "تاريخ النهاية لا يسبق البداية."));
            if (basic < 0) throw new InvalidOperationException(T("Salary cannot be negative.", "الراتب لا يكون سالباً."));
        }

        private async Task<IReadOnlyList<(int Id, string Ar, string En)>> LookupAsync(string category)
        {
            var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == category);
            return cat == null ? Array.Empty<(int, string, string)>() : await _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == cat.Id).OrderBy(v => v.SortOrder).Select(v => new ValueTuple<int, string, string>(v.Id, v.Name.NameAr, v.Name.NameEn)).ToListAsync();
        }

        /// <summary>
        /// Names for values already recorded on a row, retired ones included — the *lookup*, where
        /// <see cref="LookupAsync"/> is the *picker*. Two lists answering two questions: a
        /// deactivated catalogue value must stop being offered and must not stop being readable,
        /// and reading the picker's list to render a stored id is how a page starts printing "?" (or
        /// throwing) the day someone tidies a lookup. Tenant scoping still applies — only the
        /// soft-active filter is bypassed.
        /// </summary>
        private async Task<IReadOnlyDictionary<int, (string Ar, string En)>> LookupNamesAsync(params string[] categories)
        {
            var categoryIds = await _db.LookupCategories.AsNoTracking().Where(c => categories.Contains(c.Code)).Select(c => c.Id).ToListAsync();
            if (categoryIds.Count == 0) { return new Dictionary<int, (string, string)>(); }

            var values = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                .Where(v => v.SchoolId == _db.CurrentSchoolId && categoryIds.Contains(v.LookupCategoryId))
                .Select(v => new { v.Id, v.Name.NameAr, v.Name.NameEn })
                .ToListAsync();

            return values.ToDictionary(v => v.Id, v => (v.NameAr, v.NameEn));
        }
    }
}
