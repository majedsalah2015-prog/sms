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

        public string? NextStepCode { get; set; }

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
        public IReadOnlyList<(string StageAr, string StageEn, int Order, IReadOnlyList<(string Code, string Ar, string En)> Grades)> Stages { get; set; }
            = Array.Empty<(string, string, int, IReadOnlyList<(string, string, string)>)>();

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
