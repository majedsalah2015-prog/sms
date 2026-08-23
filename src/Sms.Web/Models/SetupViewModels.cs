using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sms.Application.Setup;
using Sms.Domain.Lookups;
using Sms.Domain.Schools;
using Sms.Domain.Setup;

namespace Sms.Web.Models
{
    /// <summary>doc/Modules/01 §8.1 Setup Wizard — stepper with completion tracking.</summary>
    public sealed class SetupWizardViewModel
    {
        public IReadOnlyList<StepState> Steps { get; set; } = Array.Empty<StepState>();

        /// <summary>
        /// The first step not yet completed — where "continue" goes. Null once nothing is left,
        /// which is also what turns the button into a badge.
        /// </summary>
        public SetupWizardSteps.Step? ResumeAt { get; set; }

        public int CompletionPercent { get; set; }

        public bool CanDeclareComplete { get; set; }

        public bool IsComplete { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public bool HasSchool { get; set; }
    }

    /// <summary>One wizard step's form. Only the fields for <see cref="StepCode"/> are bound/rendered.</summary>
    public sealed class SetupStepViewModel
    {
        public string StepCode { get; set; } = string.Empty;

        public StepState? State { get; set; }

        /// <summary>
        /// Every step's state, so the list beside the form can show which are done rather than
        /// only which one you are on. Knowing that four of nine are green is most of what a
        /// half-finished setup needs to tell its reader.
        /// </summary>
        public IReadOnlyList<StepState> AllStates { get; set; } = Array.Empty<StepState>();

        public string? NextStepCode { get; set; }

        /// <summary>
        /// The step that comes before this one, so a wizard can be walked backwards as well as
        /// forwards. Null on the first step.
        /// </summary>
        public string? PreviousStepCode { get; set; }

        /// <summary>
        /// Set by the "save and add another" button. A step that defines rows — stages, grades — is
        /// finished only when the school's whole ladder is in, and being carried to the next step
        /// after the first row means walking back for every row after it.
        /// </summary>
        public bool AddAnother { get; set; }

        /// <summary>True when this step's form adds rows, so it is worth offering to stay on it.</summary>
        public bool IsRowStep => StepCode == SetupWizardSteps.StageStructure;

        // PROFILE (ISchoolAdmin.DefineSchoolAsync)
        public int? SchoolId { get; set; }

        [Display(Name = "Name (Arabic)")] public string? NameAr { get; set; }

        [Display(Name = "Name (English)")] public string? NameEn { get; set; }

        public string? LicenseNumber { get; set; }

        public string? MinistryCode { get; set; }

        public string? City { get; set; }

        public string? AddressLine { get; set; }

        public string? ContactEmail { get; set; }

        public string? ContactPhone { get; set; }

        public string? Website { get; set; }

        [DataType(DataType.Date)] public DateTime? LicenseExpiryDate { get; set; }

        // COUNTRY_PACK
        public string? PackCode { get; set; }

        public IReadOnlyList<CountryPack> Packs { get; set; } = Array.Empty<CountryPack>();

        public CountryPack? BoundPack { get; set; }

        // CURRENCY / TIME_ZONE (School fields)
        public string? CurrencyCode { get; set; }

        public IReadOnlyList<LookupValue> Currencies { get; set; } = Array.Empty<LookupValue>();

        public string? TimeZoneId { get; set; }

        public IReadOnlyList<TimeZoneInfo> TimeZones { get; set; } = Array.Empty<TimeZoneInfo>();

        // WORKING_WEEK
        public List<DayOfWeek> WorkingDays { get; set; } = new();

        public string? FirstDayOfWeek { get; set; }

        // LANGUAGES
        public List<string> EnabledLanguages { get; set; } = new();

        public string? DefaultLanguage { get; set; }

        // CALENDAR_TYPE
        public string? CalendarType { get; set; }

        public bool HijriDisplay { get; set; }

        // NUMBERING_SERIES (read-only summary; catalog seeded by E-006/E-010)
        public int NumberingSeriesCount { get; set; }

        public IReadOnlyList<(string Code, string Entity, string Format)> NumberingSeries { get; set; } = Array.Empty<(string, string, string)>();

        // STAGE_STRUCTURE (IGradeStructureAdmin)

        /// <summary>
        /// One stage the school already has, with the grades under it. Carries the ids because the
        /// rows are editable in place: the ladder is entered once and corrected for years, and a
        /// grid you can only append to means a typo in "الصف الأول" is permanent.
        /// </summary>
        public sealed record StageRow(int Id, string NameAr, string NameEn, int Order, IReadOnlyList<GradeRow> Grades);

        public sealed record GradeRow(int Id, int StageId, string Code, string NameAr, string NameEn, int Order);

        public IReadOnlyList<StageRow> Stages { get; set; } = Array.Empty<StageRow>();

        public string? StageNameAr { get; set; }

        public string? StageNameEn { get; set; }

        public int? StageOrder { get; set; }

        public string? GradeCode { get; set; }

        public string? GradeNameAr { get; set; }

        public string? GradeNameEn { get; set; }

        public int? GradeOrder { get; set; }

        public int? ExistingStageId { get; set; }

        public IReadOnlyList<(int Id, string Ar, string En)> StageOptions { get; set; } = Array.Empty<(int, string, string)>();

        /// <summary>Optional note stored on the checklist row.</summary>
        public string? Notes { get; set; }
    }

    /// <summary>doc/Modules/01 §8.3 School settings hub.</summary>
    public sealed class SettingsHubViewModel
    {
        public IReadOnlyList<SettingKeys.Definition> Definitions { get; set; } = Array.Empty<SettingKeys.Definition>();

        public IReadOnlyList<SchoolSetting> Rows { get; set; } = Array.Empty<SchoolSetting>();

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public string ActiveGroup { get; set; } = "Regional";

        // form
        public string? Key { get; set; }

        public string? Value { get; set; }

        public int? AcademicYearId { get; set; }

        public string? Reason { get; set; }
    }

    /// <summary>doc/Modules/01 §8.4 Feature toggles.</summary>
    public sealed class FeaturesViewModel
    {
        public IReadOnlyDictionary<string, bool> States { get; set; } = new Dictionary<string, bool>();

        public string? Reason { get; set; }
    }

    /// <summary>doc/Modules/01 §8.2 Lookup management.</summary>
    /// <summary>Dedicated Nationality list editor (values of lookup category "Nationality").</summary>
    public sealed class NationalitiesViewModel
    {
        public IReadOnlyList<LookupValue> Values { get; set; } = Array.Empty<LookupValue>();

        public int NextSortOrder { get; set; }
    }

    public sealed class LookupsViewModel
    {
        public IReadOnlyList<LookupCategory> Categories { get; set; } = Array.Empty<LookupCategory>();

        public LookupCategory? Selected { get; set; }

        public IReadOnlyList<LookupValue> Values { get; set; } = Array.Empty<LookupValue>();

        // new value form
        public string? Code { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public int? SortOrder { get; set; }

        // new category form (school tier)
        public string? NewCategoryCode { get; set; }

        public string? NewCategoryAr { get; set; }

        public string? NewCategoryEn { get; set; }
    }
}

namespace Sms.Web.Models
{
    /// <summary>
    /// The country pack's own values, opened for editing on <c>/setup/pack/edit</c>.
    /// <para>
    /// VAT is carried as a percentage rather than the fraction the entity stores: 15 is what a
    /// finance officer knows the rate as, and 0.15 is what four of them in a row have typed as
    /// 15 by mistake.
    /// </para>
    /// </summary>
    public sealed class CountryPackFormViewModel
    {
        /// <summary>Identity of the pack — edited content is written as a new version under the same code.</summary>
        public string? Code { get; set; }

        /// <summary>Shown, never posted back as an edit: the engine decides the next version.</summary>
        public int Version { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public string? CountryIsoCode { get; set; }

        public string? DefaultCurrencyCode { get; set; }

        public string? DefaultTimeZoneId { get; set; }

        /// <summary>Percent, 0–100.</summary>
        public decimal VatPercent { get; set; }

        public bool HijriDisplayDefault { get; set; }

        /// <summary>Comma-separated IdType lookup codes (BR-GLB-003).</summary>
        public string? RequiredIdTypeCodes { get; set; }

        public int AuditRetentionYearsMinimum { get; set; } = 10;

        /// <summary>Comma-separated statutory report codes.</summary>
        public string? StatutoryReportCodes { get; set; }

        /// <summary>Comma-separated DayOfWeek names.</summary>
        public string? DefaultWorkingDays { get; set; }

        /// <summary>Mandatory once the school is live — rebinding after go-live is a T1 event (BR-SET-004).</summary>
        public string? Reason { get; set; }
    }
}
