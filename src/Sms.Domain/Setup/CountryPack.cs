using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Setup
{
    /// <summary>
    /// core.CountryPack (doc/Modules/01 §7, BR-SET-004). A product-defined
    /// bundle a school binds to (School.CountryPackId): VAT default, ID-type
    /// requirements (BR-GLB-003), Hijri display default, audit-retention
    /// minimum (BR-AUD-006) and the statutory report set. Product data, so
    /// deliberately NOT ISchoolScoped — same reasoning as JobDefinition:
    /// packs transcend the tenant, schools only reference them. Versioned
    /// by Code+Version, deactivate-and-new-row on content change (the
    /// WorkflowDefinition/NumberingSeries convention); old versions stay
    /// loadable so a school pinned to v1 keeps resolving.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class CountryPack : AuditableEntity, IActivatable
    {
        /// <summary>e.g. "KSA-01".</summary>
        public string Code { get; set; } = string.Empty;

        public int Version { get; set; } = 1;

        public LocalizedName Name { get; set; } = new();

        /// <summary>ISO 3166-1 alpha-2, e.g. "SA".</summary>
        public string CountryIsoCode { get; set; } = string.Empty;

        /// <summary>ISO 4217 default the wizard pre-fills (BR-GLB-112 — still picked from the Currency lookup).</summary>
        public string DefaultCurrencyCode { get; set; } = string.Empty;

        public string DefaultTimeZoneId { get; set; } = string.Empty;

        /// <summary>Standard VAT rate as a fraction (0.15 = 15%); the wizard seeds Financial.VatRate from it.</summary>
        public decimal DefaultVatRate { get; set; }

        /// <summary>BR-SET-004: Hijri display default (doc 05 dual-calendar).</summary>
        public bool HijriDisplayDefault { get; set; }

        /// <summary>Comma-separated IdType lookup codes that satisfy BR-GLB-003 for this country (e.g. "NationalId,Iqama,Passport").</summary>
        public string RequiredIdTypeCodes { get; set; } = string.Empty;

        /// <summary>BR-AUD-006: legal audit-retention floor in years (product enforces max(10, this)).</summary>
        public int AuditRetentionYearsMinimum { get; set; } = 10;

        /// <summary>Comma-separated report codes of the statutory set (doc/Modules/30 registry codes).</summary>
        public string StatutoryReportCodes { get; set; } = string.Empty;

        /// <summary>Default working days as comma-separated DayOfWeek names (e.g. "Sunday,Monday,Tuesday,Wednesday,Thursday").</summary>
        public string DefaultWorkingDays { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
