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
    public class EmployeesController : Controller
    {
        private readonly IEmployeeAdmin _employees;
        private readonly ITeacherAdmin _teachers;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;
        private readonly Sms.Web.Services.PersonPhotoService _photos;

        public EmployeesController(IEmployeeAdmin employees, ITeacherAdmin teachers, AppDbContext db, IAuditContext audit, IClock clock, Sms.Web.Services.PersonPhotoService photos)
        {
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
                query = query.Where(e => e.EmployeeNo.Contains(t) || e.FirstNameAr.Contains(t) || e.FamilyNameAr.Contains(t) || e.FirstNameEn.Contains(t) || e.FamilyNameEn.Contains(t) || (e.PrimaryIdNo != null && e.PrimaryIdNo.Contains(t)));
            }
            var total = await query.CountAsync();
            var employees = await query.OrderBy(e => e.EmployeeNo).Take(300).ToListAsync();
            var ids = employees.Select(e => e.Id).ToList();
            var current = await _db.EmployeeAssignments.AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.EffectiveToUtc == null).ToListAsync();
            var orgUnits = await _db.OrgUnits.AsNoTracking().OrderBy(u => u.NameEn).ToListAsync();
            var positions = await LookupAsync("JobTitle");
            var nats = await LookupAsync("Nationality");
            var now = _clock.UtcNow;
            var contracts = await _db.Contracts.AsNoTracking().Where(c => ids.Contains(c.EmployeeId) && c.Status == ContractStatus.Active).ToListAsync();
            var teacherIds = await _db.TeacherProfiles.AsNoTracking().Where(p => ids.Contains(p.EmployeeId)).Select(p => p.EmployeeId).ToListAsync();

            var rows = employees.Select(e =>
            {
                var a = current.FirstOrDefault(x => x.EmployeeId == e.Id);
                var ou = a == null ? null : orgUnits.FirstOrDefault(u => u.Id == a.OrgUnitId);
                var pos = a == null ? null : positions.FirstOrDefault(p => p.Id == a.PositionLookupId) is var p && p != default ? (IsArabic ? p.Ar : p.En) : null;
                var c = contracts.Where(x => x.EmployeeId == e.Id && x.StartDate <= now && x.EndDate >= now).OrderByDescending(x => x.EndDate).FirstOrDefault();
                var nat = nats.FirstOrDefault(n => n.Id == e.NationalityLookupId);
                return new EmployeeListViewModel.Row(e, pos, ou == null ? null : (IsArabic ? ou.NameAr : ou.NameEn), c, teacherIds.Contains(e.Id), nat == default ? "?" : (IsArabic ? nat.Ar : nat.En));
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
                var e = await _employees.RegisterEmployeeAsync(form.FirstNameAr!.Trim(), form.FatherNameAr!.Trim(), form.GrandfatherNameAr!.Trim(), form.FamilyNameAr!.Trim(), form.FirstNameEn!.Trim(), form.FatherNameEn!.Trim(), form.GrandfatherNameEn!.Trim(), form.FamilyNameEn!.Trim(),
                    form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, form.UserAccountId, form.PrimaryIdTypeLookupId, string.IsNullOrWhiteSpace(form.PrimaryIdNo) ? null : form.PrimaryIdNo.Trim(), form.PrimaryIdExpiry);
                if (form.OrgUnitId != null && form.PositionLookupId != null)
                {
                    await _employees.AssignPositionAsync(e.Id, form.OrgUnitId.Value, form.PositionLookupId.Value, form.ManagerEmployeeId, _clock.UtcNow.Date);
                }
                TempData["Flash"] = T($"Employee {e.EmployeeNo} registered.", $"تم تسجيل الموظف {e.EmployeeNo}.");
                return RedirectToAction(nameof(File), new { id = e.Id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
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

            return File(photo.Value.Content, photo.Value.ContentType);
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction(nameof(File), new { id, tab = "personal" });
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
                    form.Gender, form.DateOfBirth.Value, form.NationalityLookupId.Value, form.UserAccountId, form.PrimaryIdTypeLookupId, string.IsNullOrWhiteSpace(form.PrimaryIdNo) ? null : form.PrimaryIdNo.Trim(), form.PrimaryIdExpiry);
                TempData["Flash"] = T("Employee file updated.", "تم تحديث ملف الموظف.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return returnTo == "manager" ? RedirectToAction(nameof(Contracts)) : RedirectToAction(nameof(File), new { id, tab = "contracts" });
        }

        [HttpPost("{id:int}/qualifications")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.File, ActionVerb.Edit)]
        public async Task<IActionResult> AddQualification(int id, string? titleAr, string? titleEn, DateTime? dateAwarded, bool isTeachingRelevant, string? institution)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(titleEn) || dateAwarded == null) throw new InvalidOperationException(T("Both titles and the award date are required.", "العنوانان وتاريخ المنح مطلوبة."));
                await _employees.AddQualificationAsync(id, titleAr.Trim(), titleEn.Trim(), dateAwarded.Value, isTeachingRelevant, string.IsNullOrWhiteSpace(institution) ? null : institution.Trim());
                TempData["Flash"] = T("Qualification added.", "أُضيف المؤهل.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(File), new { id, tab = "qualifications" });
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            void Walk(int? parentId, int depth)
            {
                foreach (var u in units.Where(x => x.ParentOrgUnitId == parentId))
                {
                    var members = current.Where(a => a.OrgUnitId == u.Id).Select(a => (employees.FirstOrDefault(e => e.Id == a.EmployeeId), positions.FirstOrDefault(p => p.Id == a.PositionLookupId) is var p && p != default ? (IsArabic ? p.Ar : p.En) : null)).Where(x => x.Item1 != null).Select(x => (x.Item1!, x.Item2)).ToList();
                    nodes.Add(new OrgChartViewModel.Node(u, depth, members.Count, units.Count(x => x.ParentOrgUnitId == u.Id), members));
                    if (depth < 12) Walk(u.Id, depth + 1);
                }
            }
            Walk(null, 0);
            // orphans whose parent no longer exists still show, at root level
            foreach (var u in units.Where(u => u.ParentOrgUnitId != null && units.All(x => x.Id != u.ParentOrgUnitId) && nodes.All(n => n.Unit.Id != u.Id)))
            {
                nodes.Add(new OrgChartViewModel.Node(u, 0, current.Count(a => a.OrgUnitId == u.Id), 0, Array.Empty<(Employee, string?)>()));
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index), new { q, status, org, teachers });
        }

        [HttpPost("org/{unitId:int}/delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.OrgChart, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeleteOrgUnit(int unitId)
        {
            try { await _employees.DeleteOrgUnitAsync(unitId); TempData["Flash"] = T("Org unit deleted.", "حُذفت الوحدة التنظيمية."); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
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
                Qualifications = await _db.Qualifications.AsNoTracking().Where(q => q.EmployeeId == id).OrderByDescending(q => q.DateAwarded).ToListAsync(),
                TeacherProfile = profile, Teaching = teaching, CurrentLoad = load,
                HasActiveContract = contracts.Any(c => c.Status == ContractStatus.Active && c.StartDate <= now && c.EndDate >= now),
                AllowedTransitions = Enum.GetValues<EmployeeStatus>().Where(s => EmployeeStatusTransitions.CanTransition(e.Status, s)).ToList(),
                Audit = audit.Select(a => (Action: a.Action.ToString(), Field: a.FieldName, Old: a.OldValue, New: a.NewValue, At: a.OccurredAtUtc, Actor: a.ActorUserId, Reason: a.Reason)).ToList(),
                Nationalities = nats, IdTypes = idTypes, Positions = positions, OrgUnits = orgUnits,
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
            to.OrgUnitId = from.OrgUnitId; to.PositionLookupId = from.PositionLookupId; to.ManagerEmployeeId = from.ManagerEmployeeId; to.UserAccountId = from.UserAccountId;
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
    }
}
