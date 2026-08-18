using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Schools;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/02 §8: School profile (tabs), Signatories, Status console.
    /// All writes go through ISchoolAdmin; identity/status edits set the
    /// ambient audit reason (BR-SCH-002/005 are enforced by the T1 tags).
    /// Branding upload (BR-SCH-006, needs the attachment store) and the
    /// map pin widget are deferred; stages offered is shown read-only from
    /// Module 05 data (BR-SCH-003's explicit declaration is a follow-up).
    /// </summary>
    [Route("school")]
    public class SchoolController : Controller
    {
        private readonly ISchoolAdmin _schools;
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;

        public SchoolController(ISchoolAdmin schools, AppDbContext db, ITenantContext tenant, IAuditContext audit, IClock clock)
        {
            _schools = schools;
            _db = db;
            _tenant = tenant;
            _audit = audit;
            _clock = clock;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        [HttpGet("")]
        public async Task<IActionResult> Profile(string? tab = null)
        {
            var model = await BuildProfileAsync();
            model.ActiveTab = tab ?? "identity";
            return View(model);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(SchoolProfileViewModel form)
        {
            try
            {
                Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)"));
                Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                Require(form.LicenseNumber, T("License number", "رقم الترخيص"));
                Require(form.MinistryCode, T("Ministry code", "الرمز الوزاري"));
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
                // One school per tenant (ADR-2): always bind to the tenant's row, never trust a posted id.
                var existing = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
                await _schools.DefineSchoolAsync(
                    existing?.Id, form.NameAr!, form.NameEn!, form.LicenseNumber!, form.MinistryCode!,
                    form.TimeZoneId ?? "Arab Standard Time", form.CurrencyCode ?? "SAR",
                    form.AddressLine, form.City, form.ContactEmail, form.ContactPhone, form.Website, form.LicenseExpiryDate);
                TempData["Flash"] = T("School profile saved.", "تم حفظ ملف المدرسة.");
                return RedirectToAction(nameof(Profile), new { tab = form.ActiveTab });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var model = await BuildProfileAsync(form);
                return View(model);
            }
        }

        private async Task<SchoolProfileViewModel> BuildProfileAsync(SchoolProfileViewModel? form = null)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            var m = form ?? new SchoolProfileViewModel();
            if (form == null && school != null)
            {
                m.SchoolId = school.Id; m.NameAr = school.NameAr; m.NameEn = school.NameEn; m.LicenseNumber = school.LicenseNumber; m.MinistryCode = school.MinistryCode;
                m.LicenseExpiryDate = school.LicenseExpiryDate; m.AddressLine = school.AddressLine; m.City = school.City; m.Latitude = school.Latitude; m.Longitude = school.Longitude;
                m.ContactEmail = school.ContactEmail; m.ContactPhone = school.ContactPhone; m.Website = school.Website; m.TimeZoneId = school.TimeZoneId; m.CurrencyCode = school.CurrencyCode;
            }

            m.Status = school?.Status;
            m.TimeZoneId ??= school?.TimeZoneId;
            m.CurrencyCode ??= school?.CurrencyCode;
            var stages = await _db.Stages.AsNoTracking().OrderBy(s => s.SequenceOrder).ToListAsync();
            var grades = await _db.GradeLevels.AsNoTracking().ToListAsync();
            m.StagesOffered = stages.Select(s => (s.Name.NameAr, s.Name.NameEn, grades.Count(g => g.StageId == s.Id))).ToList();
            return m;
        }

        // ------------------------------------------------------------------

        [HttpGet("signatories")]
        public async Task<IActionResult> Signatories()
        {
            return View(await BuildSignatoriesAsync());
        }

        [HttpPost("signatories")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Signatories(SignatoriesViewModel form)
        {
            try
            {
                Require(form.DocumentClassCode, T("Document class", "فئة المستند"));
                Require(form.NameAr, T("Name (Arabic)", "الاسم (عربي)"));
                Require(form.NameEn, T("Name (English)", "الاسم (إنجليزي)"));
                Require(form.TitleAr, T("Title (Arabic)", "المسمى (عربي)"));
                Require(form.TitleEn, T("Title (English)", "المسمى (إنجليزي)"));
                var from = form.EffectiveFrom ?? _clock.UtcNow.Date;
                await _schools.DefineSignatoryAsync(form.DocumentClassCode!, form.NameAr!, form.NameEn!, form.TitleAr!, form.TitleEn!, DateTime.SpecifyKind(from, DateTimeKind.Utc));
                TempData["Flash"] = T("Signatory defined; the previous one was closed out (BR-SCH-004).", "تم تعريف الموقّع وإغلاق السابق (BR-SCH-004).");
                return RedirectToAction(nameof(Signatories));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var model = await BuildSignatoriesAsync();
                model.DocumentClassCode = form.DocumentClassCode; model.NameAr = form.NameAr; model.NameEn = form.NameEn; model.TitleAr = form.TitleAr; model.TitleEn = form.TitleEn; model.EffectiveFrom = form.EffectiveFrom;
                return View(model);
            }
        }

        private async Task<SignatoriesViewModel> BuildSignatoriesAsync() => new()
        {
            Signatories = await _db.Signatories.AsNoTracking().OrderBy(s => s.DocumentClassCode).ThenByDescending(s => s.EffectiveFromUtc).ToListAsync(),
            EffectiveFrom = _clock.UtcNow.Date,
        };

        // ------------------------------------------------------------------

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            return View(await BuildStatusAsync());
        }

        [HttpPost("status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Status(SchoolStatusViewModel form)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            try
            {
                if (school == null || form.Target == null)
                {
                    throw new InvalidOperationException(T("Choose a target status.", "اختر الحالة المستهدفة."));
                }

                Require(form.Reason, T("Reason", "السبب"));
                _audit.Reason = form.Reason;
                await _schools.ChangeStatusAsync(school.Id, form.Target.Value);
                TempData["Flash"] = T($"School status is now {form.Target}.", $"حالة المدرسة الآن {form.Target}.");
                return RedirectToAction(nameof(Status));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var model = await BuildStatusAsync();
                model.Target = form.Target; model.Reason = form.Reason;
                return View(model);
            }
        }

        private async Task<SchoolStatusViewModel> BuildStatusAsync()
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            var targets = school == null
                ? Array.Empty<SchoolStatus>()
                : Enum.GetValues<SchoolStatus>().Where(t => SchoolStatusTransitions.CanTransition(school.Status, t)).ToArray();
            return new SchoolStatusViewModel { School = school, AllowedTargets = targets };
        }

        private static void Require(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(T($"{field} is required.", $"الحقل {field} مطلوب."));
            }
        }
    }
}
