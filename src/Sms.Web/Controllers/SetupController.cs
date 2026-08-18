using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Grades;
using Sms.Application.Lookups;
using Sms.Application.Schools;
using Sms.Application.Setup;
using Sms.Domain.Grades;
using Sms.Domain.Lookups;
using Sms.Domain.Setup;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/01 §8 screens: Setup Wizard, School settings hub, Feature
    /// toggles, Country pack viewer, Lookup management. Every mutation goes
    /// through the E-101/E-010/E-102/E-103 admin services — the controller
    /// only binds forms, sets the ambient audit reason and renders. Reads
    /// use AppDbContext directly (tenant-filtered).
    /// </summary>
    [Route("setup")]
    public class SetupController : Controller
    {
        private readonly ISystemSetupAdmin _setup;
        private readonly ISchoolAdmin _schools;
        private readonly IGradeStructureAdmin _grades;
        private readonly ILookupAdmin _lookups;
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IAuditContext _audit;

        public SetupController(
            ISystemSetupAdmin setup, ISchoolAdmin schools, IGradeStructureAdmin grades, ILookupAdmin lookups,
            AppDbContext db, ITenantContext tenant, IAuditContext audit)
        {
            _setup = setup;
            _schools = schools;
            _grades = grades;
            _lookups = lookups;
            _db = db;
            _tenant = tenant;
            _audit = audit;
        }

        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        // ------------------------------------------------------------------
        // Wizard
        // ------------------------------------------------------------------

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var steps = await _setup.GetChecklistAsync();
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            return View(new SetupWizardViewModel
            {
                Steps = steps,
                CompletionPercent = SetupWizardEvaluator.CompletionPercent(steps),
                CanDeclareComplete = SetupWizardEvaluator.CanDeclareComplete(steps),
                IsComplete = school?.SetupCompletedAtUtc != null,
                CompletedAtUtc = school?.SetupCompletedAtUtc,
                HasSchool = school != null,
            });
        }

        [HttpPost("complete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete()
        {
            try
            {
                await _setup.DeclareSetupCompleteAsync();
                TempData["Flash"] = T("Setup declared complete. The first academic year can now be activated.", "تم إعلان اكتمال الإعداد. يمكن الآن تفعيل أول عام دراسي.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("step/{code}")]
        public async Task<IActionResult> Step(string code)
        {
            if (!SetupWizardSteps.TryGet(code, out var step))
            {
                return NotFound();
            }

            var model = await BuildStepModelAsync(step.Code);
            return View(model);
        }

        [HttpPost("step/{code}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Step(string code, SetupStepViewModel form)
        {
            if (!SetupWizardSteps.TryGet(code, out var step))
            {
                return NotFound();
            }

            try
            {
                await SaveStepAsync(step.Code, form);
                await _setup.CompleteStepAsync(step.Code, form.Notes);
                TempData["Flash"] = T($"Step \"{step.TitleEn}\" completed.", $"اكتملت خطوة \"{step.TitleAr}\".");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var model = await BuildStepModelAsync(step.Code, form);
                return View(model);
            }

            var next = (await _setup.GetChecklistAsync())
                .OrderBy(s => s.Step.Order)
                .FirstOrDefault(s => s.Status != SetupStepStatus.Completed);
            return next == null ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Step), new { code = next.Step.Code });
        }

        private async Task SaveStepAsync(string stepCode, SetupStepViewModel f)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            switch (stepCode)
            {
                case SetupWizardSteps.Profile:
                    Require(f.NameAr, "Name (Arabic)");
                    Require(f.NameEn, "Name (English)");
                    Require(f.LicenseNumber, "License number");
                    Require(f.MinistryCode, "Ministry code");
                    _audit.Reason ??= T("Setup wizard: profile", "معالج الإعداد: الملف");
                    await _schools.DefineSchoolAsync(
                        school?.Id, f.NameAr!, f.NameEn!, f.LicenseNumber!, f.MinistryCode!,
                        school?.TimeZoneId ?? "Arab Standard Time", school?.CurrencyCode ?? "SAR",
                        f.AddressLine, f.City, f.ContactEmail, f.ContactPhone, f.Website, f.LicenseExpiryDate);
                    break;

                case SetupWizardSteps.CountryPack:
                    Require(f.PackCode, "Country pack");
                    await _setup.BindCountryPackAsync(f.PackCode!, reason: T("Setup wizard: country pack", "معالج الإعداد: حزمة الدولة"));
                    break;

                case SetupWizardSteps.Currency:
                    Require(f.CurrencyCode, "Currency");
                    RequireSchool(school);
                    _audit.Reason ??= T("Setup wizard: currency", "معالج الإعداد: العملة");
                    await _schools.DefineSchoolAsync(school!.Id, school.NameAr, school.NameEn, school.LicenseNumber, school.MinistryCode,
                        school.TimeZoneId, f.CurrencyCode!, school.AddressLine, school.City, school.ContactEmail, school.ContactPhone, school.Website, school.LicenseExpiryDate);
                    break;

                case SetupWizardSteps.TimeZone:
                    Require(f.TimeZoneId, "Time zone");
                    RequireSchool(school);
                    _audit.Reason ??= T("Setup wizard: time zone", "معالج الإعداد: المنطقة الزمنية");
                    await _schools.DefineSchoolAsync(school!.Id, school.NameAr, school.NameEn, school.LicenseNumber, school.MinistryCode,
                        f.TimeZoneId!, school.CurrencyCode, school.AddressLine, school.City, school.ContactEmail, school.ContactPhone, school.Website, school.LicenseExpiryDate);
                    break;

                case SetupWizardSteps.WorkingWeek:
                    _audit.Reason ??= T("Setup wizard: working week", "معالج الإعداد: أسبوع العمل");
                    await _setup.SetSettingAsync(SettingKeys.WorkingDays, WorkingWeek.Format(f.WorkingDays));
                    if (!string.IsNullOrEmpty(f.FirstDayOfWeek))
                    {
                        await _setup.SetSettingAsync(SettingKeys.FirstDayOfWeek, f.FirstDayOfWeek);
                    }

                    break;

                case SetupWizardSteps.Languages:
                    _audit.Reason ??= T("Setup wizard: languages", "معالج الإعداد: اللغات");
                    await _setup.SetSettingAsync(SettingKeys.EnabledLanguages, string.Join(",", f.EnabledLanguages));
                    Require(f.DefaultLanguage, "Default language");
                    await _setup.SetSettingAsync(SettingKeys.DefaultLanguage, f.DefaultLanguage!);
                    break;

                case SetupWizardSteps.CalendarType:
                    Require(f.CalendarType, "Calendar type");
                    _audit.Reason ??= T("Setup wizard: calendar", "معالج الإعداد: التقويم");
                    await _setup.SetSettingAsync(SettingKeys.CalendarType, f.CalendarType!);
                    await _setup.SetSettingAsync(SettingKeys.HijriDisplay, f.HijriDisplay ? "true" : "false");
                    break;

                case SetupWizardSteps.NumberingSeries:
                    // Catalog is product-seeded (doc 08 §4); the step confirms it is present.
                    break;

                case SetupWizardSteps.StageStructure:
                    if (!string.IsNullOrWhiteSpace(f.StageNameEn) || !string.IsNullOrWhiteSpace(f.StageNameAr))
                    {
                        Require(f.StageNameAr, "Stage name (Arabic)");
                        Require(f.StageNameEn, "Stage name (English)");
                        var stage = await _grades.DefineStageAsync(f.StageNameAr!, f.StageNameEn!, f.StageOrder ?? 1, GenderPolicy.Mixed);
                        f.ExistingStageId = stage.Id;
                    }

                    if (!string.IsNullOrWhiteSpace(f.GradeCode))
                    {
                        if (f.ExistingStageId == null)
                        {
                            throw new InvalidOperationException(T("Choose or define a stage for the grade.", "اختر مرحلة أو عرّف واحدة للصف."));
                        }

                        Require(f.GradeNameAr, "Grade name (Arabic)");
                        Require(f.GradeNameEn, "Grade name (English)");
                        await _grades.DefineGradeLevelAsync(f.ExistingStageId.Value, f.GradeCode!, f.GradeNameAr!, f.GradeNameEn!, f.GradeOrder ?? 1, null, false);
                    }

                    break;
            }
        }

        private async Task<SetupStepViewModel> BuildStepModelAsync(string stepCode, SetupStepViewModel? form = null)
        {
            var states = await _setup.GetChecklistAsync();
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            var m = form ?? new SetupStepViewModel();
            m.StepCode = stepCode;
            m.State = states.Single(s => s.Step.Code == stepCode);
            m.NextStepCode = states.OrderBy(s => s.Step.Order).FirstOrDefault(s => s.Step.Order > m.State.Step.Order)?.Step.Code;

            switch (stepCode)
            {
                case SetupWizardSteps.Profile when form == null && school != null:
                    m.SchoolId = school.Id; m.NameAr = school.NameAr; m.NameEn = school.NameEn; m.LicenseNumber = school.LicenseNumber;
                    m.MinistryCode = school.MinistryCode; m.City = school.City; m.AddressLine = school.AddressLine; m.ContactEmail = school.ContactEmail;
                    m.ContactPhone = school.ContactPhone; m.Website = school.Website; m.LicenseExpiryDate = school.LicenseExpiryDate;
                    break;

                case SetupWizardSteps.CountryPack:
                    m.Packs = await _db.CountryPacks.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Code).ToListAsync();
                    m.BoundPack = await _setup.GetBoundCountryPackAsync();
                    m.PackCode ??= m.BoundPack?.Code;
                    break;

                case SetupWizardSteps.Currency:
                    m.Currencies = await LookupValuesAsync(SystemSetupAdminCurrency);
                    m.CurrencyCode ??= school?.CurrencyCode ?? (await _setup.GetBoundCountryPackAsync())?.DefaultCurrencyCode;
                    break;

                case SetupWizardSteps.TimeZone:
                    m.TimeZones = TimeZoneInfo.GetSystemTimeZones();
                    m.TimeZoneId ??= school?.TimeZoneId ?? (await _setup.GetBoundCountryPackAsync())?.DefaultTimeZoneId;
                    break;

                case SetupWizardSteps.WorkingWeek when form == null:
                    var wd = await _setup.GetSettingAsync(SettingKeys.WorkingDays);
                    m.WorkingDays = wd == null ? new List<DayOfWeek> { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday } : WorkingWeek.Parse(wd).ToList();
                    m.FirstDayOfWeek = await _setup.GetSettingAsync(SettingKeys.FirstDayOfWeek) ?? "Sunday";
                    break;

                case SetupWizardSteps.Languages when form == null:
                    m.EnabledLanguages = SettingKeys.SplitCodes(await _setup.GetSettingAsync(SettingKeys.EnabledLanguages) ?? "ar,en").ToList();
                    m.DefaultLanguage = await _setup.GetSettingAsync(SettingKeys.DefaultLanguage) ?? "ar";
                    break;

                case SetupWizardSteps.CalendarType when form == null:
                    m.CalendarType = await _setup.GetSettingAsync(SettingKeys.CalendarType) ?? "Both";
                    m.HijriDisplay = bool.TryParse(await _setup.GetSettingAsync(SettingKeys.HijriDisplay), out var h) && h;
                    break;

                case SetupWizardSteps.NumberingSeries:
                    var series = await _db.NumberingSeries.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Code).ToListAsync();
                    m.NumberingSeriesCount = series.Count;
                    m.NumberingSeries = series.Select(s => (s.Code, s.EntityName, s.FormatTemplate)).ToList();
                    break;

                case SetupWizardSteps.StageStructure:
                    var stages = await _db.Stages.AsNoTracking().OrderBy(s => s.SequenceOrder).ToListAsync();
                    var grades = await _db.GradeLevels.AsNoTracking().OrderBy(g => g.SequenceOrder).ToListAsync();
                    m.Stages = stages.Select(s => (s.Name.NameAr, s.Name.NameEn, s.SequenceOrder,
                        (IReadOnlyList<(string, string, string)>)grades.Where(g => g.StageId == s.Id).Select(g => (g.Code, g.Name.NameAr, g.Name.NameEn)).ToList())).ToList();
                    m.StageOptions = stages.Select(s => (s.Id, s.Name.NameAr, s.Name.NameEn)).ToList();
                    m.StageOrder ??= stages.Count + 1;
                    m.GradeOrder ??= grades.Count + 1;
                    break;
            }

            return m;
        }

        // ------------------------------------------------------------------
        // Settings hub
        // ------------------------------------------------------------------

        [HttpGet("settings")]
        public async Task<IActionResult> Settings(string? group = null)
        {
            return View(await BuildSettingsAsync(group ?? "Regional"));
        }

        [HttpPost("settings")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(SettingsHubViewModel form)
        {
            var group = SettingKeys.TryGet(form.Key ?? string.Empty, out var def) ? def.Group : "Regional";
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
                await _setup.SetSettingAsync(form.Key!, form.Value ?? string.Empty, form.AcademicYearId);
                TempData["Flash"] = T("Setting saved.", "تم حفظ الإعداد.");
                return RedirectToAction(nameof(Settings), new { group });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                var model = await BuildSettingsAsync(group);
                model.Key = form.Key; model.Value = form.Value; model.AcademicYearId = form.AcademicYearId; model.Reason = form.Reason;
                return View(model);
            }
        }

        private async Task<SettingsHubViewModel> BuildSettingsAsync(string group) => new()
        {
            ActiveGroup = group,
            Definitions = SettingKeys.All.OrderBy(d => d.Group).ThenBy(d => d.Key).ToList(),
            Rows = await _setup.ListSettingsAsync(),
            Years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync(),
        };

        // ------------------------------------------------------------------
        // Feature toggles
        // ------------------------------------------------------------------

        [HttpGet("features")]
        public async Task<IActionResult> Features()
        {
            return View(new FeaturesViewModel { States = await _setup.GetFeatureStatesAsync() });
        }

        [HttpPost("features/toggle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(string code, bool enabled, string? reason)
        {
            try
            {
                _audit.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                await _setup.SetFeatureAsync(code, enabled);
                TempData["Flash"] = T($"Feature {code} {(enabled ? "enabled" : "disabled")}.", $"تم {(enabled ? "تفعيل" : "تعطيل")} الميزة {code}.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Features));
        }

        // ------------------------------------------------------------------
        // Country pack viewer
        // ------------------------------------------------------------------

        [HttpGet("pack")]
        public async Task<IActionResult> Pack()
        {
            var bound = await _setup.GetBoundCountryPackAsync();
            ViewData["AllPacks"] = await _db.CountryPacks.AsNoTracking().OrderBy(p => p.Code).ThenByDescending(p => p.Version).ToListAsync();
            return View(bound);
        }

        // ------------------------------------------------------------------
        // Lookup management (E-010 engine)
        // ------------------------------------------------------------------

        [HttpGet("lookups")]
        public async Task<IActionResult> Lookups(string? category = null)
        {
            return View(await BuildLookupsAsync(category));
        }

        [HttpPost("lookups/value")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineLookupValue(LookupsViewModel form, string category)
        {
            try
            {
                var cat = await _db.LookupCategories.AsNoTracking().SingleAsync(c => c.Code == category);
                if (cat.Tier == LookupCategoryTier.ProductSeeded)
                {
                    throw new InvalidOperationException(T("Product-seeded lists are updated by product releases, not by schools (BR-SET-001).", "القوائم المزوَّدة من المنتج تُحدَّث بإصدارات المنتج وليس من المدرسة (BR-SET-001)."));
                }

                Require(form.Code, "Code");
                Require(form.NameAr, "Name (Arabic)");
                Require(form.NameEn, "Name (English)");
                await _lookups.DefineValueAsync(category, form.Code!.Trim(), form.NameAr!, form.NameEn!, form.SortOrder ?? 0);
                TempData["Flash"] = T("Value saved.", "تم حفظ القيمة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Lookups), new { category });
        }

        [HttpPost("lookups/category")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefineLookupCategory(LookupsViewModel form)
        {
            try
            {
                Require(form.NewCategoryCode, "Code");
                Require(form.NewCategoryAr, "Name (Arabic)");
                Require(form.NewCategoryEn, "Name (English)");
                await _lookups.DefineCategoryAsync(form.NewCategoryCode!.Trim(), LookupCategoryTier.SchoolManaged, form.NewCategoryAr!, form.NewCategoryEn!);
                TempData["Flash"] = T("Category created.", "تم إنشاء الفئة.");
                return RedirectToAction(nameof(Lookups), new { category = form.NewCategoryCode!.Trim() });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Lookups));
            }
        }

        [HttpPost("lookups/deactivate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateLookupValue(int id, string category)
        {
            try
            {
                var cat = await _db.LookupCategories.AsNoTracking().SingleAsync(c => c.Code == category);
                if (cat.Tier == LookupCategoryTier.ProductSeeded)
                {
                    throw new InvalidOperationException(T("Product-seeded values cannot be changed by schools (BR-SET-001).", "لا يمكن للمدرسة تعديل القيم المزوَّدة من المنتج (BR-SET-001)."));
                }

                await _lookups.DeactivateValueAsync(id);
                TempData["Flash"] = T("Value deactivated (BR-SET-002: never deleted).", "تم إلغاء تفعيل القيمة (BR-SET-002: لا حذف).");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Lookups), new { category });
        }

        private async Task<LookupsViewModel> BuildLookupsAsync(string? category)
        {
            var categories = await _db.LookupCategories.AsNoTracking().OrderBy(c => c.Tier).ThenBy(c => c.Code).ToListAsync();
            var selected = categories.FirstOrDefault(c => c.Code == category) ?? categories.FirstOrDefault();
            var values = selected == null
                ? new List<LookupValue>()
                : await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                    .Where(v => v.LookupCategoryId == selected.Id && v.SchoolId == _tenant.SchoolId)
                    .OrderBy(v => v.SortOrder).ThenBy(v => v.Code).ToListAsync();
            return new LookupsViewModel { Categories = categories, Selected = selected, Values = values, SortOrder = values.Count + 1 };
        }

        // ------------------------------------------------------------------

        private const string SystemSetupAdminCurrency = "Currency";

        private async Task<IReadOnlyList<LookupValue>> LookupValuesAsync(string categoryCode)
        {
            var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == categoryCode);
            return cat == null
                ? Array.Empty<LookupValue>()
                : await _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == cat.Id).OrderBy(v => v.SortOrder).ToListAsync();
        }

        private static void Require(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(T($"{field} is required.", $"الحقل {field} مطلوب."));
            }
        }

        private static void RequireSchool(Sms.Domain.Schools.School? school)
        {
            if (school == null)
            {
                throw new InvalidOperationException(T("Complete the School profile step first.", "أكمل خطوة ملف المدرسة أولاً."));
            }
        }
    }
}
