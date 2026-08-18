using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Setup;

namespace Sms.Application.Setup
{
    /// <summary>
    /// doc/Modules/01 §8 screens backing (Setup Wizard, School settings hub,
    /// Feature toggles, Country pack viewer — screens deferred, operations
    /// core). Standalone admin: every method saves itself. Lookup management
    /// (§8.2) is E-010's ILookupAdmin; numbering (§8.3 embed) is E-006's.
    /// </summary>
    public interface ISystemSetupAdmin
    {
        // --- Country packs (product tier, BR-SET-004) ---------------------------

        /// <summary>Defines/updates a product pack. Content edits on a pack already bound by any school deactivate it and create Version+1 (schools stay pinned until they rebind).</summary>
        Task<CountryPack> DefineCountryPackAsync(CountryPackDefinition definition, CancellationToken cancellationToken = default);

        /// <summary>Binds the working school to a pack (latest active version of <paramref name="packCode"/>). After go-live (School.Status != Setup) a non-empty <paramref name="reason"/> is mandatory (BR-SET-004, T1).</summary>
        Task BindCountryPackAsync(string packCode, string? reason = null, CancellationToken cancellationToken = default);

        Task<CountryPack?> GetBoundCountryPackAsync(CancellationToken cancellationToken = default);

        // --- Settings (BR-SET-005/007) --------------------------------------------

        /// <summary>Creates or updates the value for <paramref name="key"/>; <paramref name="academicYearId"/> only allowed on year-versionable keys, and a financial year row must target a year that hasn't ended (doc §9 "effective date ≥ today").</summary>
        Task<SchoolSetting> SetSettingAsync(string key, string value, int? academicYearId = null, CancellationToken cancellationToken = default);

        /// <summary>Resolves per SettingResolver (year row → school default → null).</summary>
        Task<string?> GetSettingAsync(string key, int? academicYearId = null, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SchoolSetting>> ListSettingsAsync(CancellationToken cancellationToken = default);

        // --- Feature toggles (BR-SET-006) -----------------------------------------

        /// <summary>Throws <see cref="Common.Exceptions.FeatureDependencyException"/> when dependencies block the change; never deletes data.</summary>
        Task SetFeatureAsync(string featureCode, bool enabled, CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(CancellationToken cancellationToken = default);

        // --- Setup wizard (BR-SET-003) ---------------------------------------------

        Task<IReadOnlyList<StepState>> GetChecklistAsync(CancellationToken cancellationToken = default);

        /// <summary>Marks a step Completed; throws <see cref="Common.Exceptions.SetupStepNotReadyException"/> when its data isn't in place. Re-completing an already-completed step is allowed (steps can be revisited).</summary>
        Task CompleteStepAsync(string stepCode, string? notes = null, CancellationToken cancellationToken = default);

        /// <summary>Stamps School.SetupCompletedAtUtc once every mandatory step is Completed; otherwise throws <see cref="Common.Exceptions.SetupIncompleteException"/>.</summary>
        Task DeclareSetupCompleteAsync(CancellationToken cancellationToken = default);

        Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Product-tier pack content (BR-SET-004 bundle).</summary>
    public sealed record CountryPackDefinition(
        string Code,
        string NameAr,
        string NameEn,
        string CountryIsoCode,
        string DefaultCurrencyCode,
        string DefaultTimeZoneId,
        decimal DefaultVatRate,
        bool HijriDisplayDefault,
        IReadOnlyList<string> RequiredIdTypeCodes,
        int AuditRetentionYearsMinimum,
        IReadOnlyList<string> StatutoryReportCodes,
        IReadOnlyList<System.DayOfWeek> DefaultWorkingDays);

    /// <summary>Read-only gate for menus/permission composition (BR-SET-006); the shell's sidebar consumes it.</summary>
    public interface IFeatureGate
    {
        Task<bool> IsEnabledAsync(string featureCode, CancellationToken cancellationToken = default);
    }
}
