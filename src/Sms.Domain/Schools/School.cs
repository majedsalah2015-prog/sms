using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Schools
{
    /// <summary>
    /// core.School — the tenant scope of all data (ADR-2, doc/Modules/02).
    /// Deliberately NOT ISchoolScoped: it defines the scope, it doesn't
    /// belong to one, same reasoning as AuditEntry/JobDefinition staying
    /// unfiltered. BR-SCH-002: identity fields are T1-audited with a
    /// mandatory reason — they're what official documents display.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class School : AuditableEntity
    {
        /// <summary>Future multi-school grouping (BR-SCH-007); null in v1's single-school reality.</summary>
        public int? SchoolGroupId { get; set; }

        [RequiresAuditReason]
        public string NameAr { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string NameEn { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string LicenseNumber { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string MinistryCode { get; set; } = string.Empty;

        /// <summary>doc/Modules/02 §14 Q1: assumed optional.</summary>
        public DateTime? LicenseExpiryDate { get; set; }

        public string? AddressLine { get; set; }

        public string? City { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public string? ContactEmail { get; set; }

        public string? ContactPhone { get; set; }

        public string? Website { get; set; }

        /// <summary>
        /// BR-SCH-006: branding assets are attachments (doc 10), so the school row keeps a pointer
        /// and re-uploading is a new version of one slot rather than a second file nobody can tell
        /// apart from the first. A plain int? with no FK, exactly as Student.PhotoAttachmentId
        /// (E-008) — the attachment is a different aggregate, and a real FK here would add another
        /// cascade path into core.School for no reader's benefit.
        /// <para>
        /// Deliberately not [RequiresAuditReason]: a logo is not an identity field, so changing it
        /// does not demand the reason BR-SCH-002 asks of a name. T1 still records the change.
        /// </para>
        /// </summary>
        public int? LogoAttachmentId { get; set; }

        /// <summary>The seal slot; same reasoning as <see cref="LogoAttachmentId"/> (BR-SCH-006).</summary>
        public int? SealAttachmentId { get; set; }

        /// <summary>Resolved by SchoolTimeZoneConverter (E-009) — a Windows or IANA id per the host's tz database.</summary>
        public string TimeZoneId { get; set; } = "Arab Standard Time";

        /// <summary>ISO 4217.</summary>
        public string CurrencyCode { get; set; } = "SAR";

        /// <summary>Active↔Suspended and reactivation require a reason (doc/Modules/02 §4); enforced here, not by convention.</summary>
        [RequiresAuditReason]
        public SchoolStatus Status { get; set; } = SchoolStatus.Setup;

        /// <summary>
        /// E-101 / BR-SET-004: the bound product country pack. Null until the
        /// wizard's CountryPack step. Changing it after go-live is a
        /// support-gated, reason-bearing change — SystemSetupAdmin enforces the
        /// reason once the school has left Setup status (the attribute alone
        /// can't express "only after go-live").
        /// </summary>
        public int? CountryPackId { get; set; }

        /// <summary>E-101 / BR-SET-003: stamped by "Setup Complete"; the first academic-year activation is gated on it.</summary>
        public DateTime? SetupCompletedAtUtc { get; set; }
    }
}
