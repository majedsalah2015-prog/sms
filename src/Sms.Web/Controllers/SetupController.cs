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
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;

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
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard, ActionVerb.View)]
        public async Task<IActionResult> Index()
        {
            var steps = await _setup.GetChecklistAsync();
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            return View(new SetupWizardViewModel
            {
                Steps = steps,
                ResumeAt = SetupWizardEvaluator.ResumeAt(steps),
                CompletionPercent = SetupWizardEvaluator.CompletionPercent(steps),
                CanDeclareComplete = SetupWizardEvaluator.CanDeclareComplete(steps),
                IsComplete = school?.SetupCompletedAtUtc != null,
                CompletedAtUtc = school?.SetupCompletedAtUtc,
                HasSchool = school != null,
            });
        }

        [HttpPost("complete")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard, ActionVerb.Configure)]
        public async Task<IActionResult> Complete()
        {
            try
            {
                await _setup.DeclareSetupCompleteAsync();
                TempData["Flash"] = T("Setup declared complete. The first academic year can now be activated.", "تم إعلان اكتمال الإعداد. يمكن الآن تفعيل أول عام دراسي.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("step/{code}")]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard, ActionVerb.View)]
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
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard, ActionVerb.Configure)]
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
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                var model = await BuildStepModelAsync(step.Code, form);
                return View(model);
            }

            // "Save and add another" keeps the operator on a step that defines rows; anything else
            // moves one step forward. SetupWizardEvaluator.NextStep owns which step that is, and
            // its summary records why this is not the same question as "the first step still
            // incomplete" — the rule this screen used to apply, and the reason it stopped advancing.
            if (form.AddAnother)
            {
                TempData["Flash"] = T("Saved. Add another, or continue when this step is done.", "تم الحفظ. أضف عنصراً آخر، أو تابع عند انتهاء الخطوة.");
                return RedirectToAction(nameof(Step), new { code = step.Code });
            }

            var next = SetupWizardEvaluator.NextStep(step.Code);
            if (next == null)
            {
                TempData["Flash"] = T($"Step \"{step.TitleEn}\" completed — that was the last one.", $"اكتملت خطوة \"{step.TitleAr}\" — وهي الأخيرة.");
                return RedirectToAction(nameof(Index));
            }

            TempData["Flash"] = T(
                $"Step \"{step.TitleEn}\" completed. Now on \"{next.TitleEn}\".",
                $"اكتملت خطوة \"{step.TitleAr}\". أنت الآن في \"{next.TitleAr}\".");
            return RedirectToAction(nameof(Step), new { code = next.Code });
        }

        /// <summary>
        /// doc/Modules/01 §8.1. Corrects a stage the school already defined, from the wizard's own
        /// grid rather than by leaving the wizard for Module 05. The ladder is entered once and
        /// lived with for years: a grid that only appends makes a typo in a stage name permanent
        /// until someone finds the other screen.
        /// <para>Its own form, because HTML has no nested forms and the step's main form is a save.</para>
        /// </summary>
        [HttpPost("step/stage/{id:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard, ActionVerb.Configure)]
        public async Task<IActionResult> UpdateStage(int id, string? nameAr, string? nameEn, int? order)
        {
            try
            {
                Require(nameAr, "Stage name (Arabic)", "اسم المرحلة (عربي)");
                Require(nameEn, "Stage name (English)", "اسم المرحلة (إنجليزي)");
                var stage = await _db.Stages.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
                if (stage == null)
                {
                    return NotFound();
                }

                _audit.Reason ??= T("Setup wizard: stage corrected", "معالج الإعداد: تصحيح مرحلة");
                await _grades.UpdateStageAsync(id, nameAr!.Trim(), nameEn!.Trim(), order ?? stage.SequenceOrder, stage.DefaultGenderPolicy);
                TempData["Flash"] = T("Stage updated.", "تم تحديث المرحلة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Step), new { code = SetupWizardSteps.StageStructure });
        }

        /// <summary>doc/Modules/01 §8.1. The same correction, one level down — and it may move the grade to another stage.</summary>
        [HttpPost("step/grade/{id:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Wizard, ActionVerb.Configure)]
        public async Task<IActionResult> UpdateGrade(int id, int? stageId, string? code, string? nameAr, string? nameEn, int? order)
        {
            try
            {
                Require(code, "Code", "الرمز");
                Require(nameAr, "Grade name (Arabic)", "اسم الصف (عربي)");
                Require(nameEn, "Grade name (English)", "اسم الصف (إنجليزي)");
                var grade = await _db.GradeLevels.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id);
                if (grade == null)
                {
                    return NotFound();
                }

                _audit.Reason ??= T("Setup wizard: grade corrected", "معالج الإعداد: تصحيح صف");
                await _grades.UpdateGradeLevelAsync(id, stageId ?? grade.StageId, code!.Trim(), nameAr!.Trim(), nameEn!.Trim(), order ?? grade.SequenceOrder);
                TempData["Flash"] = T("Grade updated.", "تم تحديث الصف.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Step), new { code = SetupWizardSteps.StageStructure });
        }

        private async Task SaveStepAsync(string stepCode, SetupStepViewModel f)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId);
            switch (stepCode)
            {
                case SetupWizardSteps.Profile:
                    Require(f.NameAr, "Name (Arabic)", "الاسم (عربي)");
                    Require(f.NameEn, "Name (English)", "الاسم (إنجليزي)");
                    Require(f.LicenseNumber, "License number", "رقم الترخيص");
                    Require(f.MinistryCode, "Ministry code", "الرمز الوزاري");
                    _audit.Reason ??= T("Setup wizard: profile", "معالج الإعداد: الملف");
                    await _schools.DefineSchoolAsync(
                        school?.Id, f.NameAr!, f.NameEn!, f.LicenseNumber!, f.MinistryCode!,
                        school?.TimeZoneId ?? "Arab Standard Time", school?.CurrencyCode ?? "SAR",
                        f.AddressLine, f.City, f.ContactEmail, f.ContactPhone, f.Website, f.LicenseExpiryDate);
                    break;

                case SetupWizardSteps.CountryPack:
                    Require(f.PackCode, "Country pack", "حزمة الدولة");
                    await _setup.BindCountryPackAsync(f.PackCode!, reason: T("Setup wizard: country pack", "معالج الإعداد: حزمة الدولة"));
                    break;

                case SetupWizardSteps.Currency:
                    Require(f.CurrencyCode, "Currency", "العملة");
                    RequireSchool(school);
                    _audit.Reason ??= T("Setup wizard: currency", "معالج الإعداد: العملة");
                    await _schools.DefineSchoolAsync(school!.Id, school.NameAr, school.NameEn, school.LicenseNumber, school.MinistryCode,
                        school.TimeZoneId, f.CurrencyCode!, school.AddressLine, school.City, school.ContactEmail, school.ContactPhone, school.Website, school.LicenseExpiryDate);
                    break;

                case SetupWizardSteps.TimeZone:
                    Require(f.TimeZoneId, "Time zone", "المنطقة الزمنية");
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
                    Require(f.DefaultLanguage, "Default language", "اللغة الافتراضية");
                    await _setup.SetSettingAsync(SettingKeys.DefaultLanguage, f.DefaultLanguage!);
                    break;

                case SetupWizardSteps.CalendarType:
                    Require(f.CalendarType, "Calendar type", "نوع التقويم");
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
                        Require(f.StageNameAr, "Stage name (Arabic)", "اسم المرحلة (عربي)");
                        Require(f.StageNameEn, "Stage name (English)", "اسم المرحلة (إنجليزي)");
                        var stage = await _grades.DefineStageAsync(f.StageNameAr!, f.StageNameEn!, f.StageOrder ?? 1, GenderPolicy.Mixed);
                        f.ExistingStageId = stage.Id;
                    }

                    if (!string.IsNullOrWhiteSpace(f.GradeCode))
                    {
                        if (f.ExistingStageId == null)
                        {
                            throw new InvalidOperationException(T("Choose or define a stage for the grade.", "اختر مرحلة أو عرّف واحدة للصف."));
                        }

                        Require(f.GradeNameAr, "Grade name (Arabic)", "اسم الصف (عربي)");
                        Require(f.GradeNameEn, "Grade name (English)", "اسم الصف (إنجليزي)");
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
            m.AllStates = states.OrderBy(s => s.Step.Order).ToList();
            m.NextStepCode = SetupWizardEvaluator.NextStep(stepCode)?.Code;
            m.PreviousStepCode = SetupWizardEvaluator.PreviousStep(stepCode)?.Code;

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
                    m.Stages = stages.Select(s => new SetupStepViewModel.StageRow(
                        s.Id, s.Name.NameAr, s.Name.NameEn, s.SequenceOrder,
                        grades.Where(g => g.StageId == s.Id)
                            .Select(g => new SetupStepViewModel.GradeRow(g.Id, g.StageId, g.Code, g.Name.NameAr, g.Name.NameEn, g.SequenceOrder))
                            .ToList())).ToList();
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
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Settings, ActionVerb.View)]
        public async Task<IActionResult> Settings(string? group = null)
        {
            return View(await BuildSettingsAsync(group ?? "Regional"));
        }

        [HttpPost("settings")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Settings, ActionVerb.Configure)]
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
                // The engine's message is English by design (it is what the log will read). A refusal
                // shown to an Arabic-speaking administrator is rebuilt from the key instead, naming the
                // setting and listing what it would have accepted.
                ModelState.AddModelError(string.Empty, ex is Sms.Application.Common.Exceptions.InvalidSettingValueException invalid
                    ? RefusalMessage(invalid.Key)
                    : UserMessage.For(ex, IsArabic));
                var model = await BuildSettingsAsync(group);
                model.Key = form.Key; model.Value = form.Value; model.AcademicYearId = form.AcademicYearId; model.Reason = form.Reason;
                return View(model);
            }
        }

        /// <summary>The refusal, plus the list of values that would have been accepted — the half a reader actually needs.</summary>
        private static string RefusalMessage(string key)
        {
            var options = SettingLabels.Options(key);
            var refused = UserMessage.For(new Sms.Application.Common.Exceptions.InvalidSettingValueException(key, string.Empty), IsArabic);
            if (options.Length == 0)
            {
                return refused;
            }

            var accepted = string.Join(" · ", options.Select(o => $"{SettingLabels.Value(key, o, IsArabic)} ({o})"));
            return refused + " " + T("Accepted: ", "المقبول: ") + accepted;
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
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Features, ActionVerb.View)]
        public async Task<IActionResult> Features()
        {
            return View(new FeaturesViewModel { States = await _setup.GetFeatureStatesAsync() });
        }

        [HttpPost("features/toggle")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Features, ActionVerb.Configure)]
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
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Features));
        }

        // ------------------------------------------------------------------
        // Country pack viewer
        // ------------------------------------------------------------------

        [HttpGet("pack")]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.ContentPack, ActionVerb.View)]
        public async Task<IActionResult> Pack()
        {
            var bound = await _setup.GetBoundCountryPackAsync();
            ViewData["AllPacks"] = await _db.CountryPacks.AsNoTracking().OrderBy(p => p.Code).ThenByDescending(p => p.Version).ToListAsync();
            return View(bound);
        }

        /// <summary>
        /// The bound pack's own values, opened for editing. The pack is product data rather than the
        /// school's, so editing one that a school is already bound to does not overwrite it: the engine
        /// retires the current version and writes version+1, and this screen then rebinds the school to
        /// what it just wrote — otherwise the edit would be saved and the school would go on resolving
        /// the old numbers.
        /// </summary>
        [HttpGet("pack/edit")]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.ContentPack, ActionVerb.Configure)]
        public async Task<IActionResult> EditPack()
        {
            var pack = await _setup.GetBoundCountryPackAsync();
            if (pack == null)
            {
                TempData["Error"] = T("No country pack is bound yet.", "لم تُربط حزمة دولة بعد.");
                return RedirectToAction(nameof(Pack));
            }

            return View(new CountryPackFormViewModel
            {
                Code = pack.Code,
                Version = pack.Version,
                NameAr = pack.Name.NameAr,
                NameEn = pack.Name.NameEn,
                CountryIsoCode = pack.CountryIsoCode,
                DefaultCurrencyCode = pack.DefaultCurrencyCode,
                DefaultTimeZoneId = pack.DefaultTimeZoneId,
                VatPercent = pack.DefaultVatRate * 100m,
                HijriDisplayDefault = pack.HijriDisplayDefault,
                RequiredIdTypeCodes = pack.RequiredIdTypeCodes,
                AuditRetentionYearsMinimum = pack.AuditRetentionYearsMinimum,
                StatutoryReportCodes = pack.StatutoryReportCodes,
                DefaultWorkingDays = pack.DefaultWorkingDays,
            });
        }

        [HttpPost("pack/edit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.ContentPack, ActionVerb.Configure)]
        public async Task<IActionResult> EditPack(CountryPackFormViewModel form)
        {
            try
            {
                Require(form.Code, "Code", "الرمز");
                Require(form.NameAr, "Name (Arabic)", "الاسم (عربي)");
                Require(form.NameEn, "Name (English)", "الاسم (إنجليزي)");

                var days = new List<DayOfWeek>();
                foreach (var code in SettingKeys.SplitCodes(form.DefaultWorkingDays ?? string.Empty))
                {
                    if (!Enum.TryParse<DayOfWeek>(code, true, out var day))
                    {
                        throw new InvalidOperationException(T($"'{code}' is not a day of week.", $"«{code}» ليس يوماً من أيام الأسبوع."));
                    }

                    days.Add(day);
                }

                if (form.VatPercent < 0 || form.VatPercent > 100)
                {
                    throw new InvalidOperationException(T("VAT must be between 0 and 100 percent.", "الضريبة بين 0 و100 بالمئة."));
                }

                var definition = new CountryPackDefinition(
                    form.Code!.Trim(), form.NameAr!.Trim(), form.NameEn!.Trim(), (form.CountryIsoCode ?? string.Empty).Trim().ToUpperInvariant(),
                    (form.DefaultCurrencyCode ?? string.Empty).Trim().ToUpperInvariant(), (form.DefaultTimeZoneId ?? string.Empty).Trim(),
                    form.VatPercent / 100m, form.HijriDisplayDefault,
                    SettingKeys.SplitCodes(form.RequiredIdTypeCodes ?? string.Empty),
                    form.AuditRetentionYearsMinimum,
                    SettingKeys.SplitCodes(form.StatutoryReportCodes ?? string.Empty),
                    days);

                _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
                var saved = await _setup.DefineCountryPackAsync(definition);

                // The write may have produced a new version; the school has to be moved onto it or the
                // edit is invisible to everything that reads the pack (BR-SET-004).
                await _setup.BindCountryPackAsync(saved.Code, form.Reason);
                TempData["Flash"] = T($"Country pack saved as v{saved.Version}.", $"حُفظت حزمة الدولة كإصدار {saved.Version}.");
                return RedirectToAction(nameof(Pack));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return View(form);
            }
        }

        // ------------------------------------------------------------------
        // Lookup management (E-010 engine)
        // ------------------------------------------------------------------

        [HttpGet("lookups")]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups, ActionVerb.View)]
        public async Task<IActionResult> Lookups(string? category = null)
        {
            return View(await BuildLookupsAsync(category));
        }

        [HttpPost("lookups/value")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups, ActionVerb.Create)]
        public async Task<IActionResult> DefineLookupValue(LookupsViewModel form, string category)
        {
            try
            {
                await RequireSchoolManagedAsync(category);
                Require(form.Code, "Code", "الرمز");
                Require(form.NameAr, "Name (Arabic)", "الاسم (عربي)");
                Require(form.NameEn, "Name (English)", "الاسم (إنجليزي)");
                await _lookups.DefineValueAsync(category, form.Code!.Trim(), form.NameAr!, form.NameEn!, form.SortOrder ?? 0);
                TempData["Flash"] = T("Value saved.", "تم حفظ القيمة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Lookups), new { category });
        }

        [HttpPost("lookups/category")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups, ActionVerb.Create)]
        public async Task<IActionResult> DefineLookupCategory(LookupsViewModel form)
        {
            try
            {
                Require(form.NewCategoryCode, "Code", "الرمز");
                Require(form.NewCategoryAr, "Name (Arabic)", "الاسم (عربي)");
                Require(form.NewCategoryEn, "Name (English)", "الاسم (إنجليزي)");
                await _lookups.DefineCategoryAsync(form.NewCategoryCode!.Trim(), LookupCategoryTier.SchoolManaged, form.NewCategoryAr!, form.NewCategoryEn!);
                TempData["Flash"] = T("Category created.", "تم إنشاء الفئة.");
                return RedirectToAction(nameof(Lookups), new { category = form.NewCategoryCode!.Trim() });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Lookups));
            }
        }

        [HttpPost("lookups/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateLookupValue(int id, string category)
        {
            try
            {
                await RequireSchoolManagedAsync(category);
                await _lookups.DeactivateValueAsync(id);
                TempData["Flash"] = T("Value deactivated (BR-SET-002: never deleted).", "تم إلغاء تفعيل القيمة (BR-SET-002: لا حذف).");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Lookups), new { category });
        }

        /// <summary>
        /// doc/Modules/01 §8.2. Corrects a value in place. The grid could add and deactivate and
        /// nothing else, so a misspelled name could only be retired and re-entered under a second
        /// code — which leaves the school's own list carrying both, and every historical record
        /// still pointing at the wrong one.
        /// <para>
        /// The code is the identity and is not editable here: rows elsewhere point at this value,
        /// and BR-SET-002 says a value is retired, never replaced underneath them.
        /// </para>
        /// </summary>
        [HttpPost("lookups/value/{id:int}")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups, ActionVerb.Edit)]
        public async Task<IActionResult> UpdateLookupValue(int id, string category, string? nameAr, string? nameEn, int? sortOrder)
        {
            try
            {
                await RequireSchoolManagedAsync(category);
                Require(nameAr, "Name (Arabic)", "الاسم (عربي)");
                Require(nameEn, "Name (English)", "الاسم (إنجليزي)");
                var value = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(v => v.Id == id && v.SchoolId == _tenant.SchoolId);
                if (value == null)
                {
                    return NotFound();
                }

                await _lookups.DefineValueAsync(category, value.Code, nameAr!.Trim(), nameEn!.Trim(), sortOrder ?? value.SortOrder);
                TempData["Flash"] = T("Value updated.", "تم تحديث القيمة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Lookups), new { category });
        }

        /// <summary>
        /// doc/Modules/01 §8.2. Puts a retired value back in the pickers. Deactivation was a
        /// one-way door on this screen — the Nationalities editor could reverse it and the generic
        /// grid could not, so retiring the wrong row meant living with it.
        /// <para>The upsert is the reactivation: <c>DefineValueAsync</c> sets the active flag.</para>
        /// </summary>
        [HttpPost("lookups/activate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups, ActionVerb.Edit)]
        public async Task<IActionResult> ActivateLookupValue(int id, string category)
        {
            try
            {
                await RequireSchoolManagedAsync(category);
                var value = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(v => v.Id == id && v.SchoolId == _tenant.SchoolId);
                if (value == null)
                {
                    return NotFound();
                }

                await _lookups.DefineValueAsync(category, value.Code, value.Name.NameAr, value.Name.NameEn, value.SortOrder);
                TempData["Flash"] = T("Value reactivated.", "تمت إعادة تفعيل القيمة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Lookups), new { category });
        }

        /// <summary>
        /// BR-SET-001: a product-seeded list is updated by product releases, not by a school.
        /// <para>
        /// Also the only place that answers "no such category" in a sentence. The three call sites
        /// each used <c>SingleAsync</c>, which for an unknown code throws "Sequence contains no
        /// elements" — a message from the query provider, in English, shown to an administrator.
        /// </para>
        /// </summary>
        private async Task RequireSchoolManagedAsync(string categoryCode)
        {
            var category = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == categoryCode);
            if (category == null)
            {
                throw new InvalidOperationException(T(
                    $"There is no lookup list with the code \"{categoryCode}\".",
                    $"لا توجد قائمة مرجعية بالرمز «{categoryCode}»."));
            }

            if (category.Tier == LookupCategoryTier.ProductSeeded)
            {
                throw new InvalidOperationException(T(
                    "Product-seeded lists are updated by product releases, not by schools (BR-SET-001).",
                    "القوائم المزوَّدة من المنتج تُحدَّث بإصدارات المنتج وليس من المدرسة (BR-SET-001)."));
            }
        }

        // ------------------------------------------------------------------
        // Nationalities (dedicated editor for the "Nationality" lookup list).
        // The list is product-seeded, but schools need to extend/correct it
        // (new nationalities, spelling) — so this screen is allowed to edit
        // it, unlike the generic Lookups screen. Values are still never
        // deleted (BR-SET-002): deactivate / reactivate only.
        // ------------------------------------------------------------------

        private const string NationalityCategory = "Nationality";

        [HttpGet("nationalities")]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Nationalities, ActionVerb.View)]
        public async Task<IActionResult> Nationalities()
        {
            var values = await LookupValuesAsync(NationalityCategory, includeInactive: true);
            return View(new NationalitiesViewModel { Values = values, NextSortOrder = values.Count == 0 ? 1 : values.Max(v => v.SortOrder) + 1 });
        }

        [HttpPost("nationalities/save")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Nationalities, ActionVerb.Create)]
        public async Task<IActionResult> SaveNationality(string? code, string? nameAr, string? nameEn, int? sortOrder)
        {
            try
            {
                Require(code, "Code", "الرمز");
                Require(nameAr, "Name (Arabic)", "الاسم بالعربية");
                Require(nameEn, "Name (English)", "الاسم بالإنجليزية");
                var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == NationalityCategory);
                if (cat == null)
                {
                    await _lookups.DefineCategoryAsync(NationalityCategory, LookupCategoryTier.ProductSeeded, "الجنسية", "Nationality");
                }

                await _lookups.DefineValueAsync(NationalityCategory, code!.Trim().ToUpperInvariant(), nameAr!.Trim(), nameEn!.Trim(), sortOrder ?? 0);
                TempData["Flash"] = T("Nationality saved.", "تم حفظ الجنسية.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Nationalities));
        }

        [HttpPost("nationalities/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Nationalities, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateNationality(int id)
        {
            try
            {
                await _lookups.DeactivateValueAsync(id);
                TempData["Flash"] = T("Nationality deactivated (kept in historical records).", "تم إلغاء تفعيل الجنسية (تبقى في السجلات التاريخية).");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Nationalities));
        }

        [HttpPost("nationalities/{id:int}/activate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Nationalities, ActionVerb.Edit)]
        public async Task<IActionResult> ActivateNationality(int id)
        {
            try
            {
                var v = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
                await _lookups.DefineValueAsync(NationalityCategory, v.Code, v.Name.NameAr, v.Name.NameEn, v.SortOrder); // upsert re-activates
                TempData["Flash"] = T("Nationality reactivated.", "تمت إعادة تفعيل الجنسية.");
            }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }
            return RedirectToAction(nameof(Nationalities));
        }

        // ------------------------------------------------------------------
        // Quick-add from any form (➕ next to a lookup drop-down). Returns JSON so
        // the page can insert the new option without losing what was typed.
        // Allowed for school-managed categories plus the product-seeded lists
        // schools legitimately extend (nationalities, job titles).
        // ------------------------------------------------------------------

        private static readonly string[] QuickAddSeededAllowlist = { NationalityCategory, "JobTitle" };

        [HttpPost("lookups/quick-add")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Setup, ScreenCatalog.Setup.Lookups, ActionVerb.Create)]
        public async Task<IActionResult> QuickAddLookupValue(string? category, string? code, string? nameAr, string? nameEn)
        {
            try
            {
                Require(category, "Category", "الفئة");
                Require(nameAr, "Name (Arabic)", "الاسم بالعربية");
                Require(nameEn, "Name (English)", "الاسم بالإنجليزية");
                var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == category);
                if (cat == null) throw new InvalidOperationException(T("Unknown lookup category.", "فئة غير معروفة."));
                if (cat.Tier == LookupCategoryTier.ProductSeeded && !QuickAddSeededAllowlist.Contains(cat.Code))
                {
                    throw new InvalidOperationException(T("Product-seeded lists are updated by product releases, not by schools (BR-SET-001).", "القوائم المزوَّدة من المنتج تُحدَّث بإصدارات المنتج وليس من المدرسة (BR-SET-001)."));
                }

                var existing = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking().Where(v => v.LookupCategoryId == cat.Id && v.SchoolId == _tenant.SchoolId).ToListAsync();
                var finalCode = string.IsNullOrWhiteSpace(code) ? GenerateCode(nameEn!, existing.Select(v => v.Code)) : code.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(code) == false && existing.Any(v => v.Code == finalCode))
                {
                    throw new InvalidOperationException(T($"Code {finalCode} already exists in this list.", $"الرمز {finalCode} موجود مسبقاً في هذه القائمة."));
                }

                var sort = existing.Count == 0 ? 1 : existing.Max(v => v.SortOrder) + 1;
                var value = await _lookups.DefineValueAsync(cat.Code, finalCode, nameAr!.Trim(), nameEn!.Trim(), sort);
                return Json(new { ok = true, id = value.Id, code = value.Code, ar = value.Name.NameAr, en = value.Name.NameEn });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { ok = false, error = UserMessage.For(ex, IsArabic) });
            }
        }

        private static string GenerateCode(string nameEn, IEnumerable<string> taken)
        {
            var baseCode = new string(nameEn.ToUpperInvariant().Where(ch => char.IsLetterOrDigit(ch)).ToArray());
            if (baseCode.Length == 0) baseCode = "VAL";
            if (baseCode.Length > 16) baseCode = baseCode.Substring(0, 16);
            var set = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
            if (!set.Contains(baseCode)) return baseCode;
            for (var i = 2; i < 1000; i++)
            {
                var candidate = baseCode + i;
                if (!set.Contains(candidate)) return candidate;
            }
            return baseCode + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
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

        private async Task<IReadOnlyList<LookupValue>> LookupValuesAsync(string categoryCode, bool includeInactive = false)
        {
            var cat = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == categoryCode);
            if (cat == null) return Array.Empty<LookupValue>();
            var query = includeInactive
                ? _db.LookupValues.IgnoreQueryFilters().AsNoTracking().Where(v => v.LookupCategoryId == cat.Id && v.SchoolId == _tenant.SchoolId)
                : _db.LookupValues.AsNoTracking().Where(v => v.LookupCategoryId == cat.Id);
            return await query.OrderBy(v => v.SortOrder).ThenBy(v => v.Code).ToListAsync();
        }

        /// <summary>
        /// Names the missing field in the reader's own language. It used to take one string and
        /// drop it into both sentences, so an Arabic-speaking administrator was told
        /// «الحقل Ministry code مطلوب» — which says what language the product was written in and
        /// very little else. The caller now supplies both names, as everything user-visible does.
        /// </summary>
        private static void Require(string? value, string fieldEn, string fieldAr)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(T($"{fieldEn} is required.", $"الحقل «{fieldAr}» مطلوب."));
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
